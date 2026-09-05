using System;
using System.IO;
using System.Runtime.Serialization;

namespace BillionsSaveManager
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string SaveDirectory;
        [DataMember] public string ArchiveDirectory;
        [DataMember] public bool AutomaticSnapshots = true;
        [DataMember(EmitDefaultValue = false)] public string Language;
        public static AppSettings Defaults()
        {
            return new AppSettings
            {
                SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"My Games\They Are Billions\Saves"),
                ArchiveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TheyAreBillionsSaveManager\Snapshots")
            };
        }
    }
}
