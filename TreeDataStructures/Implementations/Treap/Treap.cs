using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> 
    : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode) { }
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child) { }

    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right)
        Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null)
            return (null, null);

        if (Comparer.Compare(key, root.Key) > 0)
        {
            var (left, right) = Split(root.Right, key);
            root.Right = left;
            if (left != null) left.Parent = root;
            return (root, right);
        }
        else
        {
            var (left, right) = Split(root.Left, key);
            root.Left = right;
            if (right != null) right.Parent = root;
            return (left, root);
        }
    }

    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right)
        SplitLessOrEqual(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null)
            return (null, null);

        if (Comparer.Compare(root.Key, key) <= 0)
        {
            var (left, right) = SplitLessOrEqual(root.Right, key);
            root.Right = left;
            if (left != null) left.Parent = root;
            return (root, right);
        }
        else
        {
            var (left, right) = SplitLessOrEqual(root.Left, key);
            root.Left = right;
            if (right != null) right.Parent = root;
            return (left, root);
        }
    }

    protected virtual TreapNode<TKey, TValue>? Merge(
        TreapNode<TKey, TValue>? left,
        TreapNode<TKey, TValue>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            if (left.Right != null) left.Right.Parent = left;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            if (right.Left != null) right.Left.Parent = right;
            return right;
        }
    }

    public override void Add(TKey key, TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        var (leftEqual, greater) = SplitLessOrEqual(Root, key);

        var (less, equal) = Split(leftEqual, key);

        if (equal != null)
        {
            equal.Value = value;
            Root = Merge(Merge(less, equal), greater);
            return;
        }

        var newNode = CreateNode(key, value);

        Root = Merge(Merge(less, newNode), greater);

        Count++;
        OnNodeAdded(newNode);
    }

    public override bool Remove(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        var (leftEqual, greater) = SplitLessOrEqual(Root, key);

        var (less, equal) = Split(leftEqual, key);

        if (equal == null)
        {
            Root = Merge(leftEqual, greater);
            return false;
        }

        Root = Merge(less, greater);

        Count--;
        OnNodeRemoved(null, null);

        return true;
    }
}