using System;
using System.Globalization;
using UnityEngine;

// WICHTIG: Diese Klasse liegt bewusst im GLOBALEN Namespace und heisst exakt XUiC_FarmSenseHud.
// Grund: XUiFromXml::parseController loest den Controller per
// ReflectionHelpers.GetTypeWithPrefix("XUiC_" + name) -> Type.GetType(...) auf, und Type.GetType
// sucht ohne Assembly-Angabe nur in der aufrufenden Assembly (Assembly-CSharp) + mscorlib.
// Ein namespacetes Mod-Typ ist damit NICHT auffindbar; deshalb im Modlet-XML
// controller="FarmSenseHud, FarmSense" (assembly-qualifiziert) und hier globaler Namespace.
public class XUiC_FarmSenseHud : XUiController
{
    public const string WindowName = "farmSenseHud";

    public string stage = "";
    public string detail = "";
    public string warning = "";
    public float fill;
    public bool showwarning;

    // Zuletzt tatsaechlich angezeigter Inhalt (Aenderungserkennung bei Frame-Aktualisierung).
    public bool hasShown;
    public string shownStage = "";
    public string shownDetail = "";
    public string shownWarning = "";
    public float shownFill = -1f;

    static bool loggedOnce;

    public static XUiC_FarmSenseHud Find(LocalPlayerUI ui)
    {
        if (ui == null || ui.xui == null) return null;
        XUiController group = ui.xui.FindWindowGroupByName(WindowName);
        if (group == null) return null;
        return group.GetChildByType<XUiC_FarmSenseHud>();
    }

    public static void Set(LocalPlayerUI ui, string stageText, string d, string w, float f)
    {
        XUiC_FarmSenseHud c = Find(ui);
        if (c == null)
        {
            if (!loggedOnce)
            {
                loggedOnce = true;
                Log.Warning("[FarmSense] HUD-Controller nicht gefunden – Fenster 'farmSenseHud' fehlt im XUi.");
            }
            return;
        }

        c.stage = stageText;
        c.detail = d;
        c.warning = w;
        c.fill = f;
        c.showwarning = !string.IsNullOrEmpty(w);
        c.IsDirty = true;

        // Wird jetzt pro Frame aufgerufen: nur bei echter Aenderung neu binden/schreiben.
        // Unveraenderte Inhalte (gleiche Pflanze, gleicher Prozentwert) kosten so nichts.
        bool changed = !c.hasShown || stageText != c.shownStage || d != c.shownDetail
            || w != c.shownWarning || Mathf.Abs(f - c.shownFill) > 0.004f;
        if (changed)
        {
            c.hasShown = true;
            c.shownStage = stageText;
            c.shownDetail = d;
            c.shownWarning = w;
            c.shownFill = f;

            // WICHTIG: Bindings werden NICHT automatisch nachgezogen. Vanilla-Controller rufen in
            // ihrem Update() bei IsDirty selbst RefreshBindings() auf; ohne diesen Aufruf bleibt das
            // Fenster leer (Labels behalten ihren alten/leeren Inhalt).
            try { c.RefreshBindings(); }
            catch (Exception e)
            {
                if (!loggedOnce) { loggedOnce = true; Log.Warning("[FarmSense] RefreshBindings: " + e.Message); }
            }

            // Doppelt gesichert: zusaetzlich die View-Texte direkt setzen. Falls der Binding-Refresh
            // in dieser Fenstergruppe nicht laeuft, ist die Anzeige trotzdem sichtbar.
            c.ApplyToViews(stageText, d, w, f);
        }

        try
        {
            if (!ui.windowManager.IsWindowOpen(WindowName))
            {
                ui.windowManager.Open(WindowName, false);
                if (FarmSense.FarmSenseMain.Config.LogDebug) Log.Out("[FarmSense][DIAG] HUD-Fenster geoeffnet.");
            }
        }
        catch (Exception e)
        {
            Log.Warning("[FarmSense] HUD-Fenster konnte nicht geoeffnet werden: " + e.Message);
        }
    }

    public static void Hide(LocalPlayerUI ui)
    {
        if (ui == null) return;
        try
        {
            if (ui.windowManager.IsWindowOpen(WindowName)) ui.windowManager.Close(WindowName);
        }
        catch (Exception)
        {
        }
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "stage") { _value = stage; return true; }
        if (_bindingName == "detail") { _value = detail; return true; }
        if (_bindingName == "warning") { _value = warning; return true; }
        if (_bindingName == "showwarning") { _value = showwarning ? "true" : "false"; return true; }
        if (_bindingName == "fill")
        {
            _value = fill.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }

    /// <summary>Setzt die Labels/Balken direkt (unabhaengig vom Binding-Refresh).</summary>
    public void ApplyToViews(string stageText, string d, string w, float f)
    {
        try
        {
            SetLabel("lblStage", stageText);
            SetLabel("lblDetail", d);
            SetLabel("lblWarning", w);
            XUiController barCtrl = GetChildById("bar");
            if (barCtrl != null)
            {
                XUiV_Sprite sp = barCtrl.viewComponent as XUiV_Sprite;
                if (sp != null)
                {
                    sp.fillAmount = f;
                    sp.isDirty = true;
                }
            }
        }
        catch (Exception e)
        {
            if (!loggedOnce) { loggedOnce = true; Log.Warning("[FarmSense] HUD-Views: " + e.Message); }
        }
    }

    void SetLabel(string id, string text)
    {
        XUiController child = GetChildById(id);
        if (child == null) return;
        XUiV_Label lbl = child.viewComponent as XUiV_Label;
        if (lbl == null) return;
        lbl.Text = text == null ? "" : text;
        bool visible = !string.IsNullOrEmpty(text);
        if (id == "lblWarning") lbl.IsVisible = visible;
        lbl.isDirty = true;
    }
}
