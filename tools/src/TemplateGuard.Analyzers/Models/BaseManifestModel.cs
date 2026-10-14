using System.Collections.Generic;

namespace TemplateGuard.Analyzers.Models
{
    public class BaseManifestModel
    {
        public string TemplateVersion { get; set; } = "1.0.0";
        public Dictionary<string, BaseFileEntry> Files { get; set; } = new Dictionary<string, BaseFileEntry>();
    }

    public class BaseFileEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public HashSet<string> KnownMembers { get; set; } = new HashSet<string>();
        public Dictionary<string, string> MemberSyntaxHashes { get; set; } = new Dictionary<string, string>();
    }
}
