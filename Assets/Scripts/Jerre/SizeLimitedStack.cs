using System;
using UnityEngine;

namespace Jerre
{
    public class SizeLimitedStack<T>
    {

        private int maxSize;

        public int MaxSize
        {
            get { return maxSize; }
        }

        private int oldestElementIndex, newestElementIndex, count;

        public int Count
        {
            get { return count; }
        }

        private T[] elements;

        public SizeLimitedStack(int size)
        {
            maxSize = size;
            oldestElementIndex = newestElementIndex = -1;
            count = 0;
            elements = new T[size];
        }


        public void Push(T t)
        {
            newestElementIndex = (newestElementIndex + 1) % elements.Length;
            elements[newestElementIndex] = t;

            if (newestElementIndex == oldestElementIndex)
            {
                oldestElementIndex = (oldestElementIndex + 1) % elements.Length;
            }

            count++;
            if (count == 1)
            {
                oldestElementIndex = newestElementIndex;
            }
            else if (count > maxSize)
            {
                count = maxSize;
            }
        }

        public T Pop()
        {
            if (newestElementIndex == -1) throw new IndexOutOfRangeException();

            var output = elements[newestElementIndex];
            elements[newestElementIndex] = default(T);

            newestElementIndex--;
            if (newestElementIndex == -1)
            {
                newestElementIndex = elements.Length - 1;
            }

            count--;
            if (count == 0)
            {
                oldestElementIndex = newestElementIndex = -1;
            }

            return output;
        }

        public T RemoveOldest()
        {
            if (oldestElementIndex == -1) throw new IndexOutOfRangeException();

            var output = elements[oldestElementIndex];
            elements[oldestElementIndex] = default(T);
            oldestElementIndex = (oldestElementIndex + 1) % elements.Length;
            count--;
            if (count == 0)
            {
                oldestElementIndex = newestElementIndex = -1;
            }

            return output;
        }

        public T[] RemoveNElementsFromBack(int n)
        {
            if (count == 0) throw new IndexOutOfRangeException();
            if (n > count)
            {
                n = count;
            }

            var output = new T[n];
            int counter = 0, elementIndex = oldestElementIndex, outIndex = 0;
            while (counter < n)
            {
                output[outIndex] = elements[elementIndex];
                elements[elementIndex] = default(T);

                counter++;
                elementIndex = (elementIndex + 1) % elements.Length;
                outIndex++;
            }

            count -= n;
            if (count == 0)
            {
                oldestElementIndex = newestElementIndex = -1;
            }
            else
            {
                oldestElementIndex = (oldestElementIndex + n) % elements.Length;
            }

            return output;
        }

        public T[] RemoveElementsFromBackByFilter(Func<T, bool> lambda)
        {
            if (count == 0) throw new IndexOutOfRangeException();
            var temp = new T[count];

            int counter = 0, elementIndex = oldestElementIndex, outIndex = 0;
            while (counter < count)
            {
                var element = elements[elementIndex];
                if (!lambda(element))
                {
                    break;
                }
                temp[outIndex] = element;
                elements[elementIndex] = default(T);

                counter++;
                elementIndex = (elementIndex + 1) % elements.Length;
                outIndex++;
            }

            count -= counter;
            if (count == 0)
            {
                oldestElementIndex = newestElementIndex = -1;
            }
            else
            {
                oldestElementIndex = (oldestElementIndex + counter) % elements.Length;
            }

            return temp.Sublist(0, counter);
        }

        public T Peek()
        {
            if (newestElementIndex == -1) throw new IndexOutOfRangeException();

            return elements[newestElementIndex];
        }

        public T PeekFromOldest(int index)
        {
            if (index >= count) throw new IndexOutOfRangeException();
            return elements[(oldestElementIndex + index) % elements.Length];
        }

        public T[] GetArray()
        {
            var output = new T[count];
            int counter = 0, elementsIndex = newestElementIndex, ouptutIndex = 0;
            while (counter < count)
            {
                output[ouptutIndex] = elements[elementsIndex];
                counter++;
                elementsIndex--;
                if (elementsIndex == -1)
                {
                    elementsIndex = elements.Length - 1;
                }
                ouptutIndex++;
            }

            return output;
        }

        public T[] GetArrayByFilter(Func<T, bool> lambda)
        {
            if (count == 0) return new T[0];

            var temp = new T[count];


            int elementsVisitedCounter = 0, elementsStoredCounter = 0, elementIndex = oldestElementIndex, outIndex = 0;
            while (elementsVisitedCounter < count)
            {
                var element = elements[elementIndex];
                if (lambda(element))
                {
                    temp[outIndex] = element;
                    elementsStoredCounter++;
                    outIndex++;
                }

                elementIndex = (elementIndex + 1) % elements.Length;
                elementsVisitedCounter++;
            }

            return temp.Sublist(0, elementsStoredCounter);
        }

        public T FindBy(Func<T, bool> lambda)
        {
            if (count == 0) return default(T);

            var elementsVisitedCounter = 0;
            var elementIndex = oldestElementIndex;
            while (elementsVisitedCounter < count)
            {
                var element = elements[elementIndex];
                if (lambda(element))
                {
                    return element;
                }
                elementIndex = (elementIndex + 1) % elements.Length;
                elementsVisitedCounter++;
            }
            return default(T);
        }

        public bool ReplaceByFilter(Func<T, bool> lambda, T value) {
            var elementIndex = oldestElementIndex;
            for (var i = 0; i < Count; i++) {
                if (lambda(elements[elementIndex])) {
                    elements[elementIndex] = value;
                    return true;
                }
                elementIndex = (elementIndex + 1) % elements.Length;
            }
            return false;
        }
    }
}