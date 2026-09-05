using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BillionsSaveManager
{
    public class SavePair
    {
        public string SaveName;
        public string FileStem;
        public string Folder;
        public string SnapshotId;
        public DateTime SavedAtUtc;
        public long Length;
        public bool IsBackup { get { return FileStem == SaveName + "_Backup"; } }
    }

    [DataContract]
    public class FileRecord
    {
        [DataMember] public string Name;
        [DataMember] public string Sha256;
        [DataMember] public long Length;
        [DataMember] public DateTime LastWriteUtc;
    }

    [DataContract]
    public class Snapshot
    {
        [DataMember] public string Id;
        [DataMember] public string SaveName;
        [DataMember] public string Reason;
        [DataMember] public DateTime CreatedUtc;
        [DataMember] public List<FileRecord> Files = new List<FileRecord>();
        public string Folder;
    }

    public class GameObservation
    {
        public bool Running;
        public int ProcessId;
        public DateTime StartedUtc;
        public DateTime LogLastWriteUtc;
        public string LogText = "";
    }

    public interface IGameHost
    {
        GameObservation Observe();
        void Launch();
    }

    public enum RollbackState { Idle, WaitingForMenu, AwaitingLoad, Loaded, Failed }
}
