using SimpleStickyNotes.Services;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;

namespace SimpleStickyNotes
{
    public partial class App : Application
    {
        private TrayIcon _tray;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            NoteManager.Initialize();

            _tray = new TrayIcon("Simple Sticky Notes");

            // Left click = new note
            _tray.LeftClick += () => NoteManager.CreateNewNote();

            // Right click = open WPF context menu
            _tray.RightClick += ShowTrayMenu;
        }

        private void ShowTrayMenu()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var menu = new System.Windows.Controls.ContextMenu();

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "New Note",
                    Command = new RelayCommand(_ => NoteManager.CreateNewNote())
                });

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "Show All Notes",
                    Command = new RelayCommand(_ => NoteManager.ShowAllNotes())
                });

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "Bring Notes On-Screen",
                    Command = new RelayCommand(_ => NoteManager.BringAllOnScreen())
                });

                menu.Items.Add(new MenuItem
                {
                    Header = "Normalize All Notes",
                    Command = new RelayCommand(_ => NoteManager.NormalizeAllNotes())
                });

                menu.Items.Add(new System.Windows.Controls.Separator());

                menu.Items.Add(new System.Windows.Controls.MenuItem
                {
                    Header = "Exit",
                    Command = new RelayCommand(_ => Shutdown())
                });

                // Open where the mouse currently is
                menu.IsOpen = true;
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
          
            _tray?.Dispose();
        }
    }
}