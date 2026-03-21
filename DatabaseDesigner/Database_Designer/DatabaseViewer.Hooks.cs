using System;
using System.Windows;
using System.Windows.Controls;

namespace Database_Designer
{
    public partial class DatabaseViewer : Page
    {
        public void EnsureEditHook()
        {
            try
            {
                // Remove existing to avoid double-subscription
                EditData.Click -= EnsureEditHook_Click;
                EditData.Click += EnsureEditHook_Click;
            }
            catch { }
        }

        private void EnsureEditHook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedItem == null) return;

                if (selectedItem is SessionStorage.RowCreation row)
                {
                    mainPaged.CreateWindow(() => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Design, row, null, null),
                        "Edit Column: " + (row.Name ?? "Column"), true);
                    return;
                }

                if (selectedItem is SessionStorage.IndexCreation idx)
                {
                    mainPaged.CreateWindow(() => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Index, null, null, idx),
                        "Edit Index: " + (idx.IndexName ?? "Index"), true);
                    return;
                }

                if (selectedItem is SessionStorage.ReferenceOptions reference)
                {
                    mainPaged.CreateWindow(() => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Ref, null, reference, null),
                        "Edit Reference", true);
                    return;
                }
            }
            catch { }
        }
    }
}
