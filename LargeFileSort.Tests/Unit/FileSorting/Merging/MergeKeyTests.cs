using FluentAssertions;
using LargeFileSort.FileSorting.MergeChunks;

namespace LargeFileSort.Tests.Unit.FileSorting.Merging;

public class MergeKeyTests
{
	[Theory]
	[InlineData("2. banana", "2. apple", "1. apple apple")]
	[InlineData("3. apple", "1. apple", "2. apple")]
	[InlineData("1. apple", "1. banana", "1. carrot")]
	[InlineData("1. carrot", "1. banana", "1. apple")]
	[InlineData("1. apple pie", "1. apple", "1. apple apple")]
	[InlineData("2. apple", "1. banana", "3. apple")]
	[InlineData("1. apple", "1. apple", "1. apple")]
	[InlineData("1. apple", "2. apple", "3. banana", "4. banana")]
	[InlineData("5. apple", "1. banana", "2. apple", "7. carrot", "3. apple", "4. banana", "6. carrot")]
	public void PriorityQueue_WithMergeKey_ShouldReturnItemsInSortedOrder(params string[] input)
	{
		// arrange
		var items = input.Select(i => new MergeItem(i, 0)).ToList();
		
		// act
		var priorityQueue = new PriorityQueue<MergeItem, MergeKey>(
			items.Select(i => (i, new MergeKey(i))));
			
		// assert
		var previousItem = priorityQueue.Dequeue();
		while (priorityQueue.Count > 0)
		{
			var nextItem = priorityQueue.Dequeue();
			Compare(previousItem, nextItem).Should().BeLessThanOrEqualTo(0);
			previousItem = nextItem;
		}
	}

	private static int Compare(MergeItem a, MergeItem b)
	{
		var textComparision = a.Line.AsSpan(a.TextOffset).SequenceCompareTo(b.Line.AsSpan(b.TextOffset));
		if (textComparision != 0)
			return textComparision;
		
		return a.Number.CompareTo(b.Number);
	}
}