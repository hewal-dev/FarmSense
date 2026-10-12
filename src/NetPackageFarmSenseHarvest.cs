using System;
using UnityEngine;

namespace FarmSense
{
    /// <summary>
    /// Netzwerkpaket Client -> Server: "Spieler X hat bei Position P geerntet".
    /// Der Server fuehrt die Ernte autoritativ aus (Ertraege + Block entfernen).
    /// Fuer 7DTD v3.3: kein GetLength()-Override (in v3.3 entfernt).
    /// </summary>
    public class NetPackageFarmSenseHarvest : NetPackage
    {
        private int playerEntityId;
        private Vector3i blockPos;

        public NetPackageFarmSenseHarvest Setup(int _playerEntityId, Vector3i _blockPos)
        {
            playerEntityId = _playerEntityId;
            blockPos = _blockPos;
            return this;
        }

        public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }
        public override bool AllowedBeforeAuth { get { return false; } }

        public override void read(PooledBinaryReader _reader)
        {
            playerEntityId = _reader.ReadInt32();
            blockPos = new Vector3i(_reader.ReadInt16(), _reader.ReadInt16(), _reader.ReadInt16());
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(playerEntityId);
            _writer.Write((short)blockPos.x);
            _writer.Write((short)blockPos.y);
            _writer.Write((short)blockPos.z);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            try
            {
                if (_world == null) return;
                if (!ConnectionManager.Instance.IsServer) return;

                EntityPlayer player = _world.GetEntity(playerEntityId) as EntityPlayer;
                if (player == null) return;

                BlockValue bv = _world.GetBlock(blockPos);
                if (bv.isair) return;

                // Missbrauchsschutz: nur in der Naehe des Spielers ernten
                float dist = (player.position - blockPos.ToVector3()).sqrMagnitude;
                if (dist > 25f) return;

                HarvestLogic.HarvestNow(_world, blockPos, bv, player);
            }
            catch (Exception e)
            {
                Log.Warning("[FarmSense] NetPackage-Ernte fehlgeschlagen: " + e.Message);
            }
        }
    }
}
