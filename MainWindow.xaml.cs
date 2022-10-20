using ClosedXML.Excel;
using MiscExpense;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Path = System.IO.Path;

namespace MiscExpenxe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int SheetNumber = 8;
        private const int ColumnId = 2;
        private const int ColumnStartAdd = 7;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ButtonSource_Click(object sender, RoutedEventArgs e)
        {
            textBoxSource.Text = OpenFileDialog();
            buttonProcess.IsEnabled = !string.IsNullOrEmpty(textBoxSource.Text);
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            textBoxSource.Text = "";
            buttonProcess.IsEnabled = false;
        }

        private async void ButtonProcess_Click(object sender, RoutedEventArgs re)
        {
            var query = "";
            try
            {
                buttonSource.IsEnabled = false;
                buttonReset.IsEnabled = false;
                buttonProcess.IsEnabled = false;
                progress.IsIndeterminate = true;

                // copy file to avoid used file
                var folder = Path.GetDirectoryName(textBoxSource.Text);
                var files = Directory.GetFiles(folder);

                foreach (var sourceFilename in files)
                {
                    var list = new List<Class1>();
                    using (var workbook = await OpenWorkBookAsync(sourceFilename).ConfigureAwait(false))
                    {
                        // load sheets
                        var worksheet = workbook.Worksheet("L");
                        var e = 0;
                        var row = 1;
                        while (e <= 3)
                        {
                            row++;
                            var miscId = worksheet?.Cell(row, 2)?.Value?.ToString();

                            if (int.TryParse(miscId, out int id))
                            {
                                // reset error row
                                e = 0;

                                for (int i = 0; i < 3; i++)
                                {
                                    var unit = worksheet?.Cell(row, 7 + i * 2)?.Value?.ToString();
                                    var nilai = worksheet?.Cell(row, 8 + i * 2)?.Value?.ToString();
                                    if (double.TryParse(nilai, out double d))
                                    {
                                        if (d > 0)
                                        {
                                            list.Add(new Class1 { Id = id, Kategori = unit, Biaya = d });
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // increment error row
                                e++;
                            }
                        }
                    }

                    foreach (var item in list)
                    {
                        using (var connection = new SqlConnection("Server = 192.168.3.7; Database = Temp; User ID = ws; Password = online; Trusted_Connection = no; Connection Timeout = 5"))
                        {
                            query = $"INSERT INTO Table_1 (IdLayanan, Biaya, Kategori) VALUES ({item.Id}, @biaya, '{item.Kategori}')";
                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@biaya", item.Biaya);
                                await connection.OpenAsync().ConfigureAwait(false);
                                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }

                MessageBox.Show("Selesai", "Misc Expense");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Misc Expense", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    buttonSource.IsEnabled = true;
                    buttonReset.IsEnabled = true;
                    buttonProcess.IsEnabled = true;
                    progress.IsIndeterminate = false;
                });
            }
        }

        private async void ButtonProcess_Click_Bak(object sender, RoutedEventArgs e)
        {
            try
            {
                buttonSource.IsEnabled = false;
                buttonReset.IsEnabled = false;
                buttonProcess.IsEnabled = false;
                progress.IsIndeterminate = true;

                // copy file to avoid used file
                var sourceFilename = GetTempFilename(textBoxSource.Text);

                using (var workbook = await OpenWorkBookAsync(sourceFilename).ConfigureAwait(false))
                {
                    // load sheets
                    var worksheet = workbook.Worksheet(SheetNumber);

                    var miscOnlyIdAndRow = CellsColumnToList(worksheet, ColumnId);
                    var miscWithDetails = await GetMiscDetailsAsync(miscOnlyIdAndRow).ConfigureAwait(false);

                    AddMiscDetailToExcel(ref worksheet, miscWithDetails);

                    await Task.Run(() => workbook.Save()).ConfigureAwait(false);
                }

                Dispatcher.Invoke(() =>
                {
                    var filename = SaveFileDialog(textBoxSource.Text);
                    if (!string.IsNullOrEmpty(filename))
                    {
                        File.Move(sourceFilename, filename);
                        System.Diagnostics.Process.Start(filename);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Misc Expense", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    buttonSource.IsEnabled = true;
                    buttonReset.IsEnabled = true;
                    buttonProcess.IsEnabled = true;
                    progress.IsIndeterminate = false;
                });
            }
        }

        private static string OpenFileDialog()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
                return dialog.FileName;

            return null;
        }

        private async Task<XLWorkbook> OpenWorkBookAsync(string filename)
        {
            return await Task.Run(() => new XLWorkbook(filename)).ConfigureAwait(false);
        }

        private string GetTempFilename(string source)
        {
            var name = Path.GetFileName(source);
            var sourceFilename = Path.Combine(Path.GetTempPath(), name);
            File.Copy(source, sourceFilename, true);
            return sourceFilename;
        }

        private IEnumerable<Misc> CellsColumnToList(IXLWorksheet worksheet, int columnId)
        {
            var list = new List<Misc>();
            var e = 0;
            var row = 1;
            while (e <= 3)
            {
                row++;
                var miscId = worksheet?.Cell(row, columnId)?.Value?.ToString();
                if (int.TryParse(miscId, out int id))
                {
                    // reset error row
                    e = 0;

                    list.Add(new Misc
                    {
                        Id = id,
                        Row = row,
                    });
                }
                else
                {
                    // increment error row
                    e++;
                }
            }

            return list;
        }

        private async Task<IEnumerable<Misc>> GetMiscDetailsAsync(IEnumerable<Misc> miscs)
        {
            var listOfIds = string.Join(",", miscs.Select(x => x.Id));
            using (var connection = new SqlConnection("Server = 192.168.3.8; Database = MedicalSql; User ID = ws; Password = online; Trusted_Connection = no; Connection Timeout = 5"))
            {
                using (var command = new SqlCommand($"SELECT ID, Lain, BiayaLain, TGL FROM BiayaLain WHERE ID IN ({listOfIds})", connection))
                {
                    await connection.OpenAsync().ConfigureAwait(false);

                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var id = reader.GetInt32(0);
                            var lain = reader.IsDBNull(1) ? null : reader.GetString(1);
                            var biaya = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);

                            if (lain != null && biaya > 0 && miscs.FirstOrDefault(x => x.Id == id) is Misc misc)
                            {
                                var lainReplaced = GetStringReplacer(lain);
                                if (misc.Expenses.ContainsKey(lainReplaced))
                                {
                                    var b = misc.Expenses[lainReplaced];
                                    misc.Expenses[lainReplaced] = b + biaya;
                                }
                                else
                                {
                                    misc.Expenses.Add(lainReplaced, biaya);
                                }
                            }
                        }

                        return miscs;
                    }
                }
            }
        }

        private static string GetStringReplacer(string initial)
        {
            var replaces = new Dictionary<string, string>();
            foreach (var replace in MiscExpense.Properties.Settings.Default.Replace)
            {
                var split = replace.Split(';');
                if (split.Length >= 2 && split[0].ToUpperInvariant() == initial.Trim().ToUpperInvariant())
                {
                    return split[1];
                }
            }
            return initial;
        }

        private void AddMiscDetailToExcel(ref IXLWorksheet worksheet, IEnumerable<Misc> miscs)
        {
            foreach (var misc in miscs)
            {
                var col = ColumnStartAdd;
                foreach (var expense in misc.ExpensesOrdered)
                {
                    worksheet.Cell(misc.Row, col++).Value = expense.Key;
                    worksheet.Cell(misc.Row, col++).Value = expense.Value;
                }
            }
        }

        private static string SaveFileDialog(string path)
        {
            var filename = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{filename}_Processed{ext}",
            };

            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
                return dialog.FileName;

            return null;
        }
    }
}
