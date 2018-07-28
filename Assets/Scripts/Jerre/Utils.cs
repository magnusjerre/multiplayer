using System;
using System.Collections.Generic;
using UnityEngine.Analytics;
using UnityEngine.Networking;

namespace Jerre
{
    public static class Utils
    {
        /// <summary>
        /// Determines if is bit set the specified value bitnumber.
        /// </summary>
        /// <returns><c>true</c> if is bit set the specified value bitnumber; otherwise, <c>false</c>.</returns>
        /// <param name="value">Value.</param>
        /// <param name="bitnumber">Bitnumber.</param>
        public static bool IsBitSet(int value, int bitnumber)
        {
            return (value & (1 << bitnumber - 1)) != 0;
        }

        /// <summary>
        /// Tos the bit int value.
        /// </summary>
        /// <returns>The bit int value.</returns>
        /// <param name="str">String.</param>
        public static int ToBitIntValue(this String str)
        {
            var reversed = str.Reverse();
            int sum = 0;
            for (var i = 0; i < reversed.Length; i++)
            {
                if (reversed[i].Equals('1'))
                {
                    sum += (int)Math.Pow(2, i);
                }
            }
            return sum;
        }

        /// <summary>
        /// Reverse the specified str.
        /// </summary>
        /// <param name="str">String.</param>
        public static String Reverse(this String str)
        {
            if (str == null || str.Length < 2)
            {
                return str;
            }

            var charArray = str.ToCharArray();
            for (var i = 0; i < charArray.Length / 2; i++)
            {
                char temp = charArray[i];
                var swapIndex = charArray.Length - 1 - i;
                charArray[i] = charArray[swapIndex];
                charArray[swapIndex] = temp;
            }
            return new String(charArray);
        }


        /// <summary>
        /// Finds the lost package sequences, ignore any sequences that are less than 0.
        /// </summary>
        /// <returns>The lost package sequences.</returns>
        /// <param name="ack">Ack.</param>
        /// <param name="ackHistory">Ack history.</param>
        /// <param name="maxLength">Max length.</param>
        public static List<int> FindLostPackageSequences(int ack, int ackHistory, int maxLength)
        {
            List<int> output = new List<int>();
            for (int i = 1; i <= maxLength; i++)
            {
                if (!IsBitSet(ackHistory, i) && ack - i >= 0)
                {
                    output.Add(ack - i);
                }
            }
            return output;
        }

        public static List<T> AsList<T>(params T[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new List<T>();
            }

            var output = new List<T>();
            for (var i = 0; i < values.Length; i++)
            {
                output.Add(values[i]);
            }
            return output;
        }

        public static T[] AsArray<T>(List<T> values)
        {
            T[] output = new T[values.Count];
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = values[i];
            }

            return output;
        }

        public static Dictionary<int, List<T>> AddAll<T>(
            this Dictionary<int, List<T>> dictRef,
            Dictionary<int, List<T>> other)
        {
            if (dictRef == null || other == null)
            {
                throw new ArgumentNullException("dictRef: " + dictRef + ", other: " + other);
            }

            foreach (KeyValuePair<int, List<T>> kvp in other)
            {
                if (!dictRef.ContainsKey(kvp.Key))
                {
                    dictRef.Add(kvp.Key, kvp.Value);
                }
            }

            return dictRef;
        }


        public static List<T> CopyList<T>(List<T> list)
        {
            var output = new List<T>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                output.Add(list[i]);
            }

            return output;
        }

        public static T[] Sublist<T>(this T[] arr, int start, int endExclusive)
        {
            if (endExclusive == 0)
            {
                return new T[0];
            }
            if (!(start < endExclusive && start >= 0 && endExclusive <= arr.Length))
            {
                throw new IndexOutOfRangeException();
            }


            var output = new T[endExclusive - start];
            for (var i = start; i < endExclusive; i++)
            {
                output[i - start] = arr[i];
            }

            return output;
        }

        public static int Min(int a, int b)
        {
            if (a < b) return a;
            return b;
        }

        public static int Max(int a, int b)
        {
            if (a > b) return a;
            return b;
        }
    }
}
