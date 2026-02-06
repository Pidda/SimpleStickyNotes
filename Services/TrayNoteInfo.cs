using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleStickyNotes.Services
{
    public sealed class TrayNoteInfo
    {
        public TrayNoteInfo(string title, System.Guid id, bool isVisible)
        {
            Title = title;
            Id = id;
            IsVisible = isVisible;
        }

        public string Title { get; }
        public System.Guid Id { get; }
        public bool IsVisible { get; }
    }
}