using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using static Database_Designer.NodeWalker.NodeWalker.Node;

namespace Database_Designer.NodeWalker
{
    public partial class ScriptToNodeWindow : Window
    {
        public event Action<BareNode> NodeCreated;
        private string _lastContextMenuPosition;

        public ScriptToNodeWindow()
        {
            InitializeComponent();
            SignatureInput.TextChanged += SignatureInput_TextChanged;
        }

        private void SignatureInput_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var result = ParseSignature(SignatureInput.Text);
            if (result == null)
            {
                PreviewText.Text = "Invalid signature format";
                return;
            }

            var inputs = string.Join(", ", result.Value.inputs.Select(i => $"{i.Name}: {i.SemanticType}"));
            var outputs = string.Join(", ", result.Value.outputs.Select(o => $"{o.Name}: {o.SemanticType}"));
            
            PreviewText.Text = $"Inputs: {inputs}\nOutputs: {outputs}";
        }

        private (string title, List<Input> inputs, List<Output> outputs)? ParseSignature(string signature)
        {
            if (string.IsNullOrWhiteSpace(signature))
                return null;

            signature = signature.Trim();
            
            var methodMatch = Regex.Match(signature, @"^(public|private|protected|internal|static)?\s*(async)?\s*(\w+)\s+(\w+)\s*\((.*)\)");
            if (!methodMatch.Success)
            {
                var propertyMatch = Regex.Match(signature, @"^(public|private|protected|internal|static)?\s*(\w+)\s+(\w+)\s*\{\s*get");
                if (propertyMatch.Success)
                {
                    string returnType = propertyMatch.Groups[2].Value;
                    string name = propertyMatch.Groups[3].Value;
                    
                    return (name, new List<Input>(), new List<Output> 
                    { 
                        new Output("Value", GetTypeFromString(returnType), GetSemanticType(returnType)) 
                    });
                }
                return null;
            }

            string returnType2 = methodMatch.Groups[3].Value;
            string methodName = methodMatch.Groups[4].Value;
            string parameters = methodMatch.Groups[5].Value;

            var inputs = new List<Input>();
            var outputs = new List<Output>();

            if (!string.IsNullOrWhiteSpace(parameters))
            {
                var paramParts = parameters.Split(',');
                foreach (var param in paramParts)
                {
                    var paramTrim = param.Trim();
                    var paramMatch = Regex.Match(paramTrim, @"^(\w+)\s+(\w+)(?:\s*=\s*(.+))?$");
                    if (paramMatch.Success)
                    {
                        string type = paramMatch.Groups[1].Value;
                        string name = paramMatch.Groups[2].Value;
                        inputs.Add(new Input(name, GetTypeFromString(type), GetSemanticType(type), false));
                    }
                }
            }

            if (returnType2 != "void")
            {
                outputs.Add(new Output("Result", GetTypeFromString(returnType2), GetSemanticType(returnType2)));
            }

            return (methodName, inputs, outputs);
        }

        private Type GetTypeFromString(string typeName)
        {
            return typeName.ToLower() switch
            {
                "int" or "long" or "short" or "byte" => typeof(int),
                "float" or "double" or "decimal" => typeof(double),
                "string" => typeof(string),
                "bool" => typeof(bool),
                "object" => typeof(object),
                "void" => null,
                _ => typeof(object)
            };
        }

        private string GetSemanticType(string typeName)
        {
            return typeName.ToLower() switch
            {
                "int" or "long" or "short" or "byte" => "number",
                "float" or "double" or "decimal" => "number",
                "string" => "string",
                "bool" => "bool",
                "object" => "object",
                _ => "object"
            };
        }

        private void CreateNode_Click(object sender, RoutedEventArgs e)
        {
            var result = ParseSignature(SignatureInput.Text);
            if (result == null)
            {
                MessageBox.Show("Invalid method signature", "Error");
                return;
            }

            var node = new BareNode
            {
                Title = result.Value.title,
                Description = $"Generated from C# signature: {SignatureInput.Text}",
                Inputs = result.Value.inputs.ToHashSet(),
                Outputs = result.Value.outputs.ToHashSet(),
                UUID = Guid.NewGuid().ToString(),
                Logic = $"// {result.Value.title}\n// Add implementation",
                SyncType = "Sync"
            };

            NodeCreated?.Invoke(node);
            Close();
        }
    }
}