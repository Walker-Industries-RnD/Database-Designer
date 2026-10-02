using System.Collections.Generic;

namespace Database_Designer
{
    // Undo/redo for the open project. Every autosave hands over the saved
    // project text plus a "key" (the same text without window positions);
    // a new undo step is only made when the key changed.
    public sealed class ProjectHistory
    {
        public const int Limit = 100;

        private sealed class Entry
        {
            public string Full;
            public string Key;
        }

        private readonly object _lock = new();
        private readonly LinkedList<Entry> _undo = new();
        private readonly Stack<Entry> _redo = new();
        private Entry _current;
        private string _project;

        public int UndoCount { get { lock (_lock) return _undo.Count; } }
        public int RedoCount { get { lock (_lock) return _redo.Count; } }

        public void Reset(string project)
        {
            lock (_lock)
            {
                _project = project;
                _undo.Clear();
                _redo.Clear();
                _current = null;
            }
        }

        public void Record(string project, string full, string key)
        {
            lock (_lock)
            {
                if (project != _project)
                {
                    _project = project;
                    _undo.Clear();
                    _redo.Clear();
                    _current = null;
                }
                if (_current == null || _current.Key == key)
                {
                    _current = new Entry { Full = full, Key = key };
                    return;
                }
                _undo.AddLast(_current);
                if (_undo.Count > Limit) _undo.RemoveFirst();
                _redo.Clear();
                _current = new Entry { Full = full, Key = key };
            }
        }

        // Returns the project text to restore, or null when there is nothing to undo.
        public string Undo()
        {
            lock (_lock)
            {
                if (_undo.Count == 0 || _current == null) return null;
                _redo.Push(_current);
                _current = _undo.Last.Value;
                _undo.RemoveLast();
                return _current.Full;
            }
        }

        public string Redo()
        {
            lock (_lock)
            {
                if (_redo.Count == 0 || _current == null) return null;
                _undo.AddLast(_current);
                _current = _redo.Pop();
                return _current.Full;
            }
        }
    }
}
