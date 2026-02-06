using SimpleStickyNotes.Models;
using SimpleStickyNotes.Services;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace SimpleStickyNotes
{
    public partial class NoteWindow : Window
    {
        private readonly NoteModel _model;
        private bool _loaded = false;
        private const double CollapsedHeight = 30;
        private bool _suppressNextWindowDoubleClick = false;

        public NoteWindow(NoteModel model)
        {
            InitializeComponent();
            _model = model;

            ItemsList.ItemsSource = _model.Items;
            DataContext = _model;

            LoadGeometry();
            EnsureNotMaximized();

            if (_model.IsCollapsed)
            {
                Height = CollapsedHeight;
                ContentPanel.Visibility = Visibility.Collapsed;
                ResizeMode = ResizeMode.NoResize;
            }

            Loaded += (_, __) => _loaded = true;
        }

        private void EnsureNotMaximized()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;

                // Restore a sane size if needed
                if (_model.Width > SystemParameters.VirtualScreenWidth)
                    _model.Width = 300;

                if (_model.Height > SystemParameters.VirtualScreenHeight)
                    _model.Height = 200;

                Width = _model.Width;
                Height = _model.Height;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        }

        private void LoadGeometry()
        {
            Left = _model.X;
            Top = _model.Y;
            Width = _model.Width;
            Height = _model.Height;
        }
        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            if (_model.IsCollapsed)
                Expand();
            else
                Collapse();
        }

        private void Collapse()
        {
            _model.ExpandedHeight = Height;
            Height = CollapsedHeight;

            CollapseButton.Content = "▔";

            ContentPanel.Visibility = Visibility.Collapsed;
            ResizeMode = ResizeMode.NoResize;

            _model.IsCollapsed = true;
            NoteManager.SaveNotes();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Delete this note permanently?",
                "Delete Note",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                NoteManager.DeleteNote(_model);
            }
        }

        private void Expand()
        {
            Height = _model.ExpandedHeight > CollapsedHeight
                ? _model.ExpandedHeight
                : 200;

            ContentPanel.Visibility = Visibility.Visible;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            CollapseButton.Content = "▁";

            _model.IsCollapsed = false;
            NoteManager.SaveNotes();
        }

        // -----------------------------
        // DOUBLE CLICK TO ENTER EDIT MODE
        // -----------------------------
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_suppressNextWindowDoubleClick)
            {
                _suppressNextWindowDoubleClick = false;
                e.Handled = true;
                return;
            }

            if (EditBox.Visibility == Visibility.Visible)
                return;

            EnterEditMode();
        }

        private void TitleText_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _suppressNextWindowDoubleClick = true;
                e.Handled = true;
                BeginTitleEdit();
            }
        }

        private void BeginTitleEdit()
        {
            TitleText.Visibility = Visibility.Collapsed;
            TitleEditor.Visibility = Visibility.Visible;

            TitleEditor.Focus();
            TitleEditor.SelectAll();
        }

        private void TitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            EndTitleEdit();
        }

        private void TitleEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                EndTitleEdit();
                e.Handled = true;
            }
        }

        private void EndTitleEdit()
        {
            TitleEditor.Visibility = Visibility.Collapsed;
            TitleText.Visibility = Visibility.Visible;

            NoteManager.SaveNotes();
        }

        private void EnterEditMode()
        {
            EditBox.Text = ToEditableText(_model);

            EditBox.Visibility = Visibility.Visible;
            ItemsList.Visibility = Visibility.Collapsed;

            EditBox.Focus();
            EditBox.CaretIndex = EditBox.Text.Length;
        }


        private string ToEditableText(NoteModel model)
        {
            return string.Join("\n", model.Items.Select(i =>
                i.IsChecked ? "[x] " + i.Text : "[ ] " + i.Text
            ));
        }


        // -----------------------------
        // EXIT EDIT MODE
        // -----------------------------
        private void EditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        private void EditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                ExitEditMode();
        }

        private void ExitEditMode()
        {
            if (EditBox.Visibility != Visibility.Visible)
                return;

            ApplyEditedText();

            EditBox.Visibility = Visibility.Collapsed;
            ItemsList.Visibility = Visibility.Visible;
        }


        // -----------------------------
        // PARSE CHECKBOX LINES
        // -----------------------------
        private void ApplyEditedText()
        {
            _model.Items.Clear();

            var lines = EditBox.Text.Split('\n');

            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^\[( |x)\]\s*(.*)$", RegexOptions.IgnoreCase);

                if (m.Success)
                {
                    _model.Items.Add(new NoteItem
                    {
                        IsChecked = m.Groups[1].Value.ToLower() == "x",
                        Text = m.Groups[2].Value
                    });
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    _model.Items.Add(new NoteItem
                    {
                        IsChecked = false,
                        Text = line
                    });
                }
            }

            ItemsList.Items.Refresh();
            NoteManager.SaveNotes();
        }


        // -----------------------------
        // CHECKBOX CHANGE
        // -----------------------------
        private void CheckboxChanged(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;

            NoteManager.SaveNotes();
        }


        // -----------------------------
        // DRAGGING
        // -----------------------------
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }


        // -----------------------------
        // SAVE GEOMETRY
        // -----------------------------
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (!_loaded) return;

            NoteManager.UpdateNotePositionSize(_model, Left, Top, Width, Height);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (!_loaded) return;

            NoteManager.UpdateNotePositionSize(_model, Left, Top, Width, Height);
        }


        // -----------------------------
        // BUTTONS
        // -----------------------------
        private void Hide_Click(object sender, RoutedEventArgs e)
            => NoteManager.HideNote(_model);

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            NoteManager.HideNote(_model);
        }

    }
}