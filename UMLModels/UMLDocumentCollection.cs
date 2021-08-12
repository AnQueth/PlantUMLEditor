using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace UMLModels
{
    public class UMLDocumentCollection
    {
        public UMLDocumentCollection()
        {
            ClassDocuments = new();
            SequenceDiagrams = new();
            ComponentDiagrams = new();
        }

        public LockedList<UMLClassDiagram> ClassDocuments { get; set; }

        public LockedList<UMLComponentDiagram> ComponentDiagrams { get; set; }
        public LockedList<UMLSequenceDiagram> SequenceDiagrams { get; set; }
    }

    public class LockedList<T> : IEnumerable<T> where T : UMLDiagram
    {
        private readonly List<T> _list = new();
        private readonly object _lock = new();

        public LockedList()
        {
        }
        public LockedList(IEnumerable<T> items)
        {
            _list.AddRange(items);
        }
        public T? Find(Predicate<T> pred)
        {
            lock (_lock)
                return _list.Find(pred);
        }

        public void RemoveAll(Predicate<T> pred)
        {
            lock (_lock)
                _list.RemoveAll(pred);
        }

        public void Clear()
        {
            lock (_lock)
                _list.Clear();
        }
        public void Add(T item)
        {
            lock (_lock)
            {
                _list.Add(item);
            }
        }

        public void Remove(T item)
        {
            lock (_lock)
                _list.Remove(item);
        }

        class LockedListEnumerator : IEnumerator<T>
        {
            private readonly List<T> _list;
            private readonly object _lockingObject;
            private T? _current;
            private int _pointer;

            public LockedListEnumerator(object lockingObject, List<T> list)
            {
                _list = list;
                _lockingObject = lockingObject;
                _current = default;
                _pointer = -1;

                Monitor.Enter(lockingObject);

            }


            public T Current => _current ?? throw new ArgumentNullException(nameof(Current));

            object? IEnumerator.Current => _current;

            public void Dispose()
            {
                Monitor.Exit(_lockingObject);
            }

            public bool MoveNext()
            {
                ++_pointer;
                if (_pointer >= _list.Count)
                {
                    return false;
                }
                _current = _list[_pointer];
                return true;

            }

            public void Reset()
            {
                _pointer = -1;
                _current = default;
            }
        }


        public IEnumerator<T> GetEnumerator()
        {
            return new LockedListEnumerator(_lock, _list);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new LockedListEnumerator(_lock, _list);
        }
    }
}