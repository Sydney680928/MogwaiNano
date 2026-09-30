using System;

namespace MogwaiNano.Objects
{
    public class MOGStack
    {
        private MOGObject[] _items;
        private int _top;

        private const int InitialCapacity = 16;

        public MOGStack()
        {
            _items = new MOGObject[InitialCapacity];
            _top = 0;
        }

        public int Count => _top;

        public MOGObject[] Items => _items;

        public void Push(MOGObject item)
        {
            if (_top >= _items.Length)
                Grow();

            _items[_top] = item;
            _top++;
        }

        public MOGObject Pop()
        {
            if (_top > 0)
            {
                _top--;

                var item = _items[_top];
                _items[_top] = null;

                return item;
            }
            return null;
        }

        public Type[] Sign(int count)
        {
            if (_top < count)
                return new Type[0];

            var t = new Type[count];
            var index = 0;
            
            for (int i = _top - 1; i >= _top - count; i--)
                t[index++] = _items[i].GetType();
            
            return t;
        }

        public void Clear()
        {
            for (int i = 0; i < _top; i++)
                _items[i] = null;

            _top = 0;
        }

        public bool Swap()
        {
            if (_top > 1)
            {
                var tmp = _items[_top - 1];
                _items[_top - 1] = _items[_top - 2];
                _items[_top - 2] = tmp;
                
                return true;
            }

            return false;
        }

        public void Dup()
        {
            if (_top > 0)
                Push(_items[_top - 1]);
        }

        public void Drop()
        {
            if (_top > 0)
            {
                _top--;
                _items[_top] = null;
            }
        }

        private void Grow()
        {
            var newItems = new MOGObject[_items.Length * 2];
            Array.Copy(_items, newItems, _items.Length);
            _items = newItems;
        }
    }
}
