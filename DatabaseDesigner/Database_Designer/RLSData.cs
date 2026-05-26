using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Database_Designer
{
    public class RLSData
    {
        public class PolicyRole
        {
            public string Name { get; set; } = "Untitled Role";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<TablePolicies> Tables { get; set; } = new();
            public byte[] IconBytes { get; set; } = null;
            [JsonIgnore]
            public int TotalPolicies => Tables.Sum(t => t.Policies.Count);
        }
        public class TablePolicies
        {
            public string TableName { get; set; } = "Table Name";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<Policy> Policies { get; set; } = new();
        }
        public class Policy
        {
            public string Name { get; set; } = "Policy Name";
            public string Category { get; set; } = "Base Server";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public string Tag { get; set; } = "";
        }

        public List<PolicyRole> Roles { get; set; } = new();

        public int SelectedRoleIndex { get; set; } = -1;
        public int SelectedTableIndex { get; set; } = -1;

        [JsonIgnore]
        public PolicyRole SelectedRole => SelectedRoleIndex >= 0 && SelectedRoleIndex < Roles.Count
            ? Roles[SelectedRoleIndex] : null;
        [JsonIgnore]
        public TablePolicies SelectedTable => SelectedRole != null && SelectedTableIndex >= 0 && SelectedTableIndex < SelectedRole.Tables.Count
            ? SelectedRole.Tables[SelectedTableIndex] : null;

        [JsonIgnore]
        public static readonly Dictionary<string, Color> CategoryColors = new()
        {
            ["Base Server"]   = Color.FromRgb(0xFF, 0x4C, 0x4C),
            ["Profiles"]      = Color.FromRgb(0xFF, 0x4C, 0x4C),
            ["Communication"] = Color.FromRgb(0x7B, 0x3C, 0xFF),
            ["Economy"]       = Color.FromRgb(0x39, 0xA9, 0x5F),
        };

        public void SeedDefaults()
        {
            string[] roleNames = { "Standard Users", "Admin Users", "Moderator Users", "Bot Users" };
            foreach (var n in roleNames)
            {
                var role = new PolicyRole { Name = n };
                Roles.Add(role);
            }
            if (Roles.Count > 0)
                SelectedRoleIndex = 0;
        }
    }
}