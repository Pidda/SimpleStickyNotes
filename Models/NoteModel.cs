using System;
using System.Collections.Generic;

namespace SimpleStickyNotes.Models
{


    public class NoteModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public List<NoteItem> Items { get; set; } = new();

        public string Title { get; set; } = "Note";
        public bool IsCollapsed { get; set; } = false;
        public double ExpandedHeight { get; set; } = 200;

        public double X { get; set; } = 200;
        public double Y { get; set; } = 200;
        public double Width { get; set; } = 250;
        public double Height { get; set; } = 200;

        public bool IsVisible { get; set; } = true;
    }
}