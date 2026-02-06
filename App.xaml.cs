using SimpleStickyNotes.Services;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;

namespace SimpleStickyNotes
{
    public partial class App : Application
    {
        private TrayIcon _tray;
            string iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "simplestickynotes.ico");

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            NoteManager.Initialize();


            _tray = new TrayIcon("Simple Sticky Notes", iconPath);

            _tray.LeftClick += () => NoteManager.CreateNewNote();
            _tray.RightClick += ShowTrayMenu;
        }

        private void ShowTrayMenu()
        {
            Current.Dispatcher.Invoke(() =>
            {
                var menu = new System.Windows.Controls.ContextMenu();

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "New Note",
                    Command = new RelayCommand(_ => Services.NoteManager.CreateNewNote())
                });

                menu.Items.Add(new System.Windows.Controls.Separator());

                // Notes list
                var notes = Services.NoteManager.GetTrayNoteList();

                if (notes.Count == 0)
                {
                    menu.Items.Add(new System.Windows.Controls.MenuItem
                    {
                        Header = "(No notes)",
                        IsEnabled = false
                    });
                }
                else
                {
                    // Optional: limit to avoid a massive menu
                    foreach (var n in notes.Take(30))
                    {
                        var item = new System.Windows.Controls.MenuItem
                        {
                            Header = n.Title,
                            //IsCheckable = false,
                            //IsChecked = n.IsVisible,
                            Command = new RelayCommand(_ => Services.NoteManager.ShowNote(n.Id))
                        };

                        menu.Items.Add(item);
                    }

                    if (notes.Count > 30)
                    {
                        menu.Items.Add(new System.Windows.Controls.MenuItem
                        {
                            Header = $"(+{notes.Count - 30} more…)",
                            IsEnabled = false
                        });
                    }
                }

                menu.Items.Add(new System.Windows.Controls.Separator());

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "Bring Notes On-Screen",
                    Command = new RelayCommand(_ => Services.NoteManager.BringAllOnScreen())
                });

                menu.Items.Add(new System.Windows.Controls.Separator());

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "Exit",
                    Command = new RelayCommand(_ => Shutdown())
                });

                menu.IsOpen = true;
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
          
            _tray?.Dispose();
        }

    }
}