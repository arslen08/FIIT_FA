using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new RbNode<TKey, TValue>(key, value) { Color = RbColor.Red };
    }

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        FixInsert(newNode);
    }

    private void FixInsert(RbNode<TKey, TValue> node)
    {
        while (node.Parent is RbNode<TKey, TValue> parent && parent.Color == RbColor.Red)
        {
            if (parent.Parent is not RbNode<TKey, TValue> grandparent)
                break;

            RbNode<TKey, TValue>? uncle = GetUncle(node);

            if (uncle?.Color == RbColor.Red)
            {
                parent.Color = RbColor.Black;
                uncle.Color = RbColor.Black;
                grandparent.Color = RbColor.Red;

                node = grandparent;
                continue;
            }

            if (parent == grandparent.Left)
            {
                if (node == parent.Right)
                {
                    RotateLeft(parent);
                    (node, parent) = (parent, node);
                }

                RotateRight(grandparent);
                parent.Color = RbColor.Black;
                grandparent.Color = RbColor.Red;
            }
            else
            {
                if (node == parent.Left)
                {
                    RotateRight(parent);
                    (node, parent) = (parent, node);
                }

                RotateLeft(grandparent);
                parent.Color = RbColor.Black;
                grandparent.Color = RbColor.Red;
            }

            break;
        }

        if (Root != null)
            Root.Color = RbColor.Black;
    }

    private RbNode<TKey, TValue>? GetUncle(RbNode<TKey, TValue> node)
    {
        if (node.Parent?.Parent is not RbNode<TKey, TValue> grandparent)
            return null;

        return node.Parent == grandparent.Left
            ? grandparent.Right as RbNode<TKey, TValue>
            : grandparent.Left as RbNode<TKey, TValue>;
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        if (child?.Color == RbColor.Red)
        {
            child.Color = RbColor.Black;
            return;
        }

        FixDelete(parent, child);
    }

    private void FixDelete(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? node)
    {
        while (node != Root && (node == null || node.Color == RbColor.Black))
        {
            if (parent == null)
                break;

            bool isLeft = ReferenceEquals(node, parent.Left);

            RbNode<TKey, TValue>? sibling = isLeft 
                ? parent.Right as RbNode<TKey, TValue> 
                : parent.Left as RbNode<TKey, TValue>;

            if (sibling == null)
                break;

            if (sibling.Color == RbColor.Red)
            {
                sibling.Color = RbColor.Black;
                parent.Color = RbColor.Red;

                if (isLeft)
                    RotateLeft(parent);
                else
                    RotateRight(parent);

                sibling = isLeft 
                    ? parent.Right as RbNode<TKey, TValue> 
                    : parent.Left as RbNode<TKey, TValue>;

                if (sibling == null) break;
            }

            bool leftBlack  = sibling.Left  == null || ((RbNode<TKey, TValue>)sibling.Left).Color  == RbColor.Black;
            bool rightBlack = sibling.Right == null || ((RbNode<TKey, TValue>)sibling.Right).Color == RbColor.Black;

            if (leftBlack && rightBlack)
            {
                sibling.Color = RbColor.Red;
                node = parent;
                parent = node.Parent as RbNode<TKey, TValue>;
                continue;
            }

            if (isLeft)
            {
                if (sibling.Right == null || ((RbNode<TKey, TValue>)sibling.Right).Color == RbColor.Black)
                {
                    if (sibling.Left != null)
                        ((RbNode<TKey, TValue>)sibling.Left).Color = RbColor.Black;

                    sibling.Color = RbColor.Red;
                    RotateRight(sibling);

                    sibling = parent.Right as RbNode<TKey, TValue>;
                    if (sibling == null) break;
                }

                sibling.Color = parent.Color;
                parent.Color = RbColor.Black;
                if (sibling.Right != null)
                    ((RbNode<TKey, TValue>)sibling.Right).Color = RbColor.Black;

                RotateLeft(parent);
            }
            else
            {
                if (sibling.Left == null || ((RbNode<TKey, TValue>)sibling.Left).Color == RbColor.Black)
                {
                    if (sibling.Right != null)
                        ((RbNode<TKey, TValue>)sibling.Right).Color = RbColor.Black;

                    sibling.Color = RbColor.Red;
                    RotateLeft(sibling);

                    sibling = parent.Left as RbNode<TKey, TValue>;
                    if (sibling == null) break;
                }

                sibling.Color = parent.Color;
                parent.Color = RbColor.Black;
                if (sibling.Left != null)
                    ((RbNode<TKey, TValue>)sibling.Left).Color = RbColor.Black;

                RotateRight(parent);
            }

            node = Root;
        }

        if (node != null)
            node.Color = RbColor.Black;
    }
}