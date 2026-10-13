using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// EquatableArray compares by element so cached pipeline values match; a default instance acts as an empty array
public sealed class EquatableArrayTests
{
    [Test]
    [Arguments(new[] { "a", "b" }, new[] { "a", "b" }, true)]
    [Arguments(new string[0], new string[0], true)]
    [Arguments(new[] { "a", "b" }, new[] { "a", "c" }, false)]
    [Arguments(new[] { "a", "b" }, new[] { "a" }, false)]
    [Arguments(new[] { "a" }, new[] { "A" }, false)]
    public async Task Equals_TwoArrays_ComparesElementsInOrder(string[] left, string[] right, bool expected)
    {
        var first = new EquatableArray<string>(left);
        var second = new EquatableArray<string>(right);

        await Assert.That(first.Equals(second)).IsEqualTo(expected);
        await Assert.That(first.Equals((object)second)).IsEqualTo(expected);
        await Assert.That(first.GetHashCode() == second.GetHashCode()).IsEqualTo(expected);
    }

    [Test]
    public async Task Equals_DefaultInstance_EqualsEmptyArray()
    {
        var empty = new EquatableArray<string>([]);
        EquatableArray<string> unset = default;

        await Assert.That(unset.Equals(empty)).IsTrue();
        await Assert.That(empty.Equals(unset)).IsTrue();
        await Assert.That(unset.GetHashCode()).IsEqualTo(empty.GetHashCode());
        await Assert.That(unset.Count).IsEqualTo(0);
        await Assert.That(unset.ToArray()).IsEmpty();
        await Assert.That(() => unset[0]).Throws<IndexOutOfRangeException>();
    }

    [Test]
    public async Task Equals_NullItems_ComparesAndHashesThem()
    {
        var first = new EquatableArray<string>([null!, "a"]);
        var second = new EquatableArray<string>([null!, "a"]);

        await Assert.That(first.Equals(second)).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
        await Assert.That(first.Equals(new EquatableArray<string>(["a", null!]))).IsFalse();
    }

    [Test]
    public async Task Equals_OtherObject_ReturnsFalse()
    {
        var array = new EquatableArray<string>(["a"]);

        await Assert.That(array.Equals("a")).IsFalse();
        await Assert.That(array.Equals((object?)null)).IsFalse();
    }

    [Test]
    public async Task GetEnumerator_Items_YieldsEachItemInOrder()
    {
        var array = new EquatableArray<string>(["a", "b"]);
        var untyped = new List<object>();
        var enumerator = ((IEnumerable)array).GetEnumerator();
        while (enumerator.MoveNext())
        {
            untyped.Add(enumerator.Current);
        }

        await Assert.That(array.Count).IsEqualTo(2);
        await Assert.That(array[1]).IsEqualTo("b");
        await Assert.That(array.ToArray()).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(untyped).IsEquivalentTo(new object[] { "a", "b" });
    }
}
