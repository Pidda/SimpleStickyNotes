using SimpleStickyNotes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Application = System.Windows.Application;

namespace SimpleStickyNotes.Services
{
    public static class NoteManager
    {
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // Virtual screen metrics (multi-monitor combined desktop)
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;



        private static readonly List<NoteModel> Notes = new();
        private static readonly Dictionary<Guid, NoteWindow> Windows = new();

        private static readonly string SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimpleStickyNotes");

        private static readonly string SavePath = Path.Combine(SaveFolder, "notes.json");

        private static readonly string BackupFolder = Path.Combine(SaveFolder, "Backups");

        private static readonly object SaveLock = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static void Initialize()
        {
            LoadNotes();

            // Create windows for visible notes
            foreach (var note in Notes.Where(n => n.IsVisible))
                CreateWindow(note);

            // Create a default note if none exist
            if (Notes.Count == 0)
                CreateNewNote();
        }

        private static Rect GetVirtualScreenRect()
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            return new Rect
            {
                Left = x,
                Top = y,
                Right = x + w,
                Bottom = y + h
            };
        }

        public static void NormalizeAllNotes()
        {
            foreach (var note in Notes)
            {
                note.Width = Math.Min(note.Width, 400);
                note.Height = Math.Min(note.Height, 300);

                if (Windows.TryGetValue(note.Id, out var win))
                {
                    win.WindowState = WindowState.Normal;
                    win.Width = note.Width;
                    win.Height = note.Height;
                }
            }

            SaveNotes();
        }

        private static void ClampNoteToScreen(NoteModel note)
        {
            var r = GetVirtualScreenRect();

            // Keep at least a small portion visible so you can grab it
            const int visibleMargin = 40;

            // Ensure width/height are reasonable
            if (note.Width < 120) note.Width = 120;
            if (note.Height < 60) note.Height = 60;

            // Clamp X/Y so the window stays inside bounds
            double maxX = r.Right - visibleMargin;
            double maxY = r.Bottom - visibleMargin;

            double minX = r.Left;
            double minY = r.Top;

            if (note.X > maxX) note.X = maxX;
            if (note.Y > maxY) note.Y = maxY;
            if (note.X < minX) note.X = minX;
            if (note.Y < minY) note.Y = minY;
        }

        public static void CreateNewNote()
        {
            var note = new NoteModel
            {
                X = 200 + Notes.Count * 30,
                Y = 200 + Notes.Count * 30
            };

            Notes.Add(note);
            SaveNotes();

            CreateWindow(note);
        }

        private static void CreateWindow(NoteModel note)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ClampNoteToScreen(note);

                var win = new NoteWindow(note);
                win.Show();

                Windows[note.Id] = win;
            });
        }

        public static void DeleteNote(NoteModel note)
        {
            if (Windows.TryGetValue(note.Id, out var win))
            {
                win.Close();
                Windows.Remove(note.Id);
            }

            Notes.Remove(note);
            SaveNotes();
        }

        public static void HideNote(NoteModel note)
        {
            note.IsVisible = false;

            if (Windows.TryGetValue(note.Id, out var win))
                win.Hide();

            SaveNotes();
        }


        public static void SaveNotes()
        {
            lock (SaveLock)
            {
                try
                {
                    Directory.CreateDirectory(SaveFolder);
                    Directory.CreateDirectory(BackupFolder);

                    var json = JsonSerializer.Serialize(Notes, JsonOptions);

                    // 1) Write to temp first (atomic write pattern)
                    var tmpPath = SavePath + ".tmp";
                    File.WriteAllText(tmpPath, json);

                    // 2) Make a timestamped backup of the CURRENT notes.json (before replacing)
                    if (File.Exists(SavePath))
                    {
                        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var backupPath = Path.Combine(BackupFolder, $"notes_{stamp}.json");
                        File.Copy(SavePath, backupPath, overwrite: false);
                    }

                    // 3) Replace notes.json with temp (atomic on Windows)
                    // If notes.json doesn't exist yet, just move tmp into place.
                    if (File.Exists(SavePath))
                    {
                        var oldPath = SavePath + ".old";
                        File.Replace(tmpPath, SavePath, oldPath, ignoreMetadataErrors: true);

                        // Optional: keep the .old as "last known good" or delete it
                        // File.Delete(oldPath);
                    }
                    else
                    {
                        File.Move(tmpPath, SavePath);
                    }

                    // 4) Prune backups (keep last N)
                    PruneBackups(keepLast: 50);
                }
                catch
                {
                    // For now swallow; ideally log to a file in SaveFolder.
                }
            }
        }

        private static void PruneBackups(int keepLast)
        {
            try
            {
                var backups = new DirectoryInfo(BackupFolder)
                    .GetFiles("notes_*.json")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                foreach (var file in backups.Skip(keepLast))
                {
                    try { file.Delete(); } catch { }
                }
            }
            catch { }
        }



        private static void LoadNotes()
        {
            try
            {
                if (TryLoadFromFile(SavePath))
                    return;

                // If main load failed, try newest backup
                Directory.CreateDirectory(BackupFolder);
                var newestBackup = new DirectoryInfo(BackupFolder)
                    .GetFiles("notes_*.json")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault();

                if (newestBackup != null && TryLoadFromFile(newestBackup.FullName))
                {
                    // Restore it as the current notes.json
                    File.Copy(newestBackup.FullName, SavePath, overwrite: true);
                }
            }
            catch
            {
                // start empty
            }
        }

        private static bool TryLoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<List<NoteModel>>(json, JsonOptions);
                if (loaded == null) return false;

                Notes.Clear();
                Notes.AddRange(loaded);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ShowAllNotes()
        {
            foreach (var note in Notes)
            {
                note.IsVisible = true;

                if (Windows.TryGetValue(note.Id, out var win))
                {
                    win.Show();
                    win.Activate();
                }
                else
                {
                    CreateWindow(note);
                }
            }

            SaveNotes();
        }

        public static void BringAllOnScreen()
        {
            foreach (var note in Notes)
            {
                ClampNoteToScreen(note);

                if (Windows.TryGetValue(note.Id, out var win))
                {
                    win.Left = note.X;
                    win.Top = note.Y;
                    win.Width = note.Width;
                    win.Height = note.Height;
                    win.Show();
                    win.Activate();
                }
            }

            SaveNotes();
        }

        public static void UpdateNotePositionSize(NoteModel note, double x, double y, double width, double height)
        {
            note.X = x;
            note.Y = y;
            note.Width = width;
            note.Height = height;

            SaveNotes();
        }
    }
}