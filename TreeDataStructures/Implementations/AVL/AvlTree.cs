using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> 
    : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        BalanceUp(newNode.Parent);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        BalanceUp(parent);
    }

    private void BalanceUp(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            UpdateHeight(node);

            int balance = BalanceFactor(node);

            if (balance > 1)
            {
                if (BalanceFactor(node.Left!) >= 0)
                {
                    RotateRight(node);
                }
                else
                {
                    RotateLeft(node.Left!);
                    RotateRight(node);
                }
            }
            else if (balance < -1)
            {
                if (BalanceFactor(node.Right!) <= 0)
                {
                    RotateLeft(node);
                }
                else
                {
                    RotateRight(node.Right!);
                    RotateLeft(node);
                }
            }

            node = node.Parent;
        }
    }

    private void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
    }

    private int Height(AvlNode<TKey, TValue>? node)
        => node?.Height ?? 0;

    private int BalanceFactor(AvlNode<TKey, TValue>? node)
        => node == null ? 0 : Height(node.Left) - Height(node.Right);

    protected new void RotateLeft(AvlNode<TKey, TValue> x)
    {
        var y = x.Right!;
        base.RotateLeft(x);

        UpdateHeight(x);
        UpdateHeight(y);
    }

    protected new void RotateRight(AvlNode<TKey, TValue> y)
    {
        var x = y.Left!;
        base.RotateRight(y);

        UpdateHeight(y);
        UpdateHeight(x);
    }
}