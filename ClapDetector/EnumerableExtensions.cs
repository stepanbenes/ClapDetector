using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClapDetector.Extensions
{
	/// <summary>
	/// Taken from MeshEditor.Common project
	/// </summary>
	public static class EnumerableExtensions
	{
		/// <summary>
		/// Returns empty enumerable if source sequence is null.
		/// </summary>
		public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source) => source ?? Enumerable.Empty<T>();

		/// <summary>
		/// Returns null reference if source sequence is empty.
		/// </summary>
		public static IEnumerable<T> NullIfEmpty<T>(this IEnumerable<T> source) => (source != null && source.Any()) ? source : null;

		/// <summary>
		/// Get min value in sequence, ignore value passed as argument
		/// </summary>
		/// <param name="ignore">Value to ignore in comparisons</param>
		/// <returns>Min value in sequence or null if empty or full of ignore values</returns>
		public static T? Min<T>(this IEnumerable<T> source, T ignore) where T : struct, IComparable<T> => source.extremeWithIgnore(ignore, -1);

		/// <summary>
		/// Get max value in sequence, ignore value passed as argument
		/// </summary>
		/// <param name="ignore">Value to ignore in comparisons</param>
		/// <returns>Max value in sequence or null if empty or full of ignore values</returns>
		public static T? Max<T>(this IEnumerable<T> source, T ignore) where T : struct, IComparable<T> => source.extremeWithIgnore(ignore, +1);

		/// <summary>
		/// Split an IEnumerable<T> into fixed-sized chunks.
		/// see: http://stackoverflow.com/questions/13709626/split-an-ienumerablet-into-fixed-sized-chunks-return-an-ienumerableienumerab
		/// </summary>
		public static IEnumerable<IReadOnlyList<T>> Partition<T>(this IEnumerable<T> items, int size)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));
			if (size <= 0)
				throw new ArgumentOutOfRangeException(nameof(size));
			return new PartitionHelper<T>(items, size);
		}

		public static bool IsOrdered<T>(this IEnumerable<T> source) => IsOrdered(source, key => key);

		public static bool IsOrdered<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var comparer = Comparer<TKey>.Default;
			using (var iterator = source.GetEnumerator())
			{
				if (!iterator.MoveNext())
					return true;

				TKey current = keySelector(iterator.Current);

				while (iterator.MoveNext())
				{
					TKey next = keySelector(iterator.Current);
					if (comparer.Compare(current, next) > 0)
						return false;

					current = next;
				}
			}

			return true;
		}

		public static bool IsOrderedDescending<T>(this IEnumerable<T> source) => IsOrderedDescending(source, key => key);

		public static bool IsOrderedDescending<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var comparer = Comparer<TKey>.Default;
			using (var iterator = source.GetEnumerator())
			{
				if (!iterator.MoveNext())
					return true;

				TKey current = keySelector(iterator.Current);

				while (iterator.MoveNext())
				{
					TKey next = keySelector(iterator.Current);
					if (comparer.Compare(current, next) < 0)
						return false;

					current = next;
				}
			}

			return true;
		}

		public static IEnumerable<int> IndicesOfMinElements(this IEnumerable<double> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return indicesOfExtremeElements(source, -1);
		}

		public static IEnumerable<int> IndicesOfMaxElements(this IEnumerable<double> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return indicesOfExtremeElements(source, +1);
		}

		public static int IndexOfFirst<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));

			int index = 0;
			foreach (var element in source)
			{
				if (predicate(element))
					return index;
				index += 1;
			}
			return -1;
		}

		public static int IndexOfSingle<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));

			int index = 0;
			bool found = false;
			foreach (var element in source)
			{
				if (predicate(element))
				{
					if (found)
					{
						throw new InvalidOperationException("Sequence contains more than one element satysfying the condition.");
					}
					else
					{
						found = true;
					}
				}
				else
				{
					index += 1;
				}
			}
			return found ? index : -1;
		}

		#region Private members

		private static T? extremeWithIgnore<T>(this IEnumerable<T> source, T ignore, int extremeSign) where T : struct, IComparable<T>
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			bool minSet = false;
			T extreme = default;
			using (var iterator = source.GetEnumerator())
			{
				while (iterator.MoveNext())
				{
					T current = iterator.Current;
					if (!current.Equals(ignore))
					{
						if (!minSet)
						{
							extreme = current;
							minSet = true;
						}
						else if (extreme.CompareTo(current) * extremeSign < 0)
						{
							extreme = current;
						}
					}
				}
			}
			return (minSet) ? extreme : (T?)null;
		}

		private static IEnumerable<T> concatIterator<T>(T extraElement, IEnumerable<T> source, bool insertAtStart)
		{
			if (insertAtStart)
				yield return extraElement;
			foreach (var e in source)
				yield return e;
			if (!insertAtStart)
				yield return extraElement;
		}

		private sealed class PartitionHelper<T> : IEnumerable<IReadOnlyList<T>>
		{
			readonly IEnumerable<T> items;
			readonly int partitionSize;
			bool hasMoreItems;

			internal PartitionHelper(IEnumerable<T> i, int ps)
			{
				items = i;
				partitionSize = ps;
			}

			public IEnumerator<IReadOnlyList<T>> GetEnumerator()
			{
				using (var enumerator = items.GetEnumerator())
				{
					hasMoreItems = enumerator.MoveNext();
					while (hasMoreItems)
						yield return getNextBatch(enumerator).ToList();
				}
			}

			IEnumerable<T> getNextBatch(IEnumerator<T> enumerator)
			{
				for (int i = 0; i < partitionSize; ++i)
				{
					yield return enumerator.Current;
					hasMoreItems = enumerator.MoveNext();
					if (!hasMoreItems)
						yield break;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		private static IEnumerable<int> indicesOfExtremeElements(IEnumerable<double> source, int extremeSign)
		{
			Debug.Assert(source != null);

			List<int> indices = new List<int>();
			int currentIndex = 0;
			double? currentExtreme = null;

			foreach (double currentValue in source)
			{
				if (!double.IsNaN(currentValue))
				{
					if (currentExtreme.HasValue)
					{
						int compareResult = currentExtreme.Value.CompareTo(currentValue);
						if (compareResult == 0) // equal to current extreme
						{
							indices.Add(currentIndex);
						}
						else if (compareResult * extremeSign < 0) // next is extreme-er :) than currentExtreme, reset is needed
						{
							currentExtreme = currentValue;
							indices.Clear();
							indices.Add(currentIndex);
						}
						// else currentExtreme holds
					}
					else
					{
						currentExtreme = currentValue;
						indices.Add(currentIndex);
					}
				}
				currentIndex += 1;
			}
			return indices;
		}

		#endregion
	}
}
