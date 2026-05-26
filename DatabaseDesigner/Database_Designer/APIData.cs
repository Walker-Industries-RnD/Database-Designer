using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Database_Designer
{
    public class APIData
    {
        public class APIModule
        {
            public string Name { get; set; } = "Untitled Module";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<APIEndpoint> Endpoints { get; set; } = new();
            public byte[] IconBytes { get; set; } = null;
            [JsonIgnore]
            public int TotalFunctions => Endpoints.Sum(e => e.Functions.Count);
        }
        public class APIEndpoint
        {
            public string Name { get; set; } = "Endpoint Name";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<APIFunction> Functions { get; set; } = new();
        }
        public class APIFunction
        {
            public string Name { get; set; } = "Function Name";
            public string Verb { get; set; } = "GET";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public string Tag { get; set; } = "";
        }

        public List<APIModule> Modules { get; set; } = new();

        public int SelectedModuleIndex { get; set; } = -1;
        public int SelectedEndpointIndex { get; set; } = -1;

        [JsonIgnore]
        public APIModule SelectedModule => SelectedModuleIndex >= 0 && SelectedModuleIndex < Modules.Count
            ? Modules[SelectedModuleIndex] : null;
        [JsonIgnore]
        public APIEndpoint SelectedEndpoint => SelectedModule != null && SelectedEndpointIndex >= 0 && SelectedEndpointIndex < SelectedModule.Endpoints.Count
            ? SelectedModule.Endpoints[SelectedEndpointIndex] : null;

        [JsonIgnore]
        public static readonly Dictionary<string, Color> VerbColors = new()
        {
            ["GET"]    = Color.FromRgb(0x39, 0xA9, 0x5F),
            ["POST"]   = Color.FromRgb(0xFF, 0xA9, 0x4C),
            ["PUT"]    = Color.FromRgb(0x4C, 0x9C, 0xFF),
            ["DELETE"] = Color.FromRgb(0xFF, 0x4C, 0x4C),
            ["PATCH"]  = Color.FromRgb(0xBB, 0x8F, 0xFF),
        };

        public void SeedDefaults()
        {
            string[] moduleNames = { "Auth", "Users", "Data", "Admin" };
            foreach (var n in moduleNames)
            {
                var module = new APIModule { Name = n };
                Modules.Add(module);
            }
            if (Modules.Count > 0)
                SelectedModuleIndex = 0;
        }
    }
}