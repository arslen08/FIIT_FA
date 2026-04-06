using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => throw new NotImplementedException();
    public ICollection<TValue> Values => throw new NotImplementedException();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        TNode? newNode = CreateNode(key, value);
        if (Root == null)
        {
            Root = newNode;
            Count++;
            OnNodeAdded(newNode);
            return;
        }

        TNode? parent = null;
        TNode? curr = Root;

        while (curr != null)
        {
            parent = curr;
            int cmp = Comparer.Compare(key, curr.Key);

            if (cmp == 0)
            {
                curr.Value = value; 
                return;
            }

            curr = cmp < 0 ? curr.Left : curr.Right;
        }

        newNode.Parent = parent;

        if (Comparer.Compare(key, parent!.Key) < 0)
        {
            parent.Left = newNode;
        }
        else
        {
            parent.Right = newNode;
        }

        Count++;
        OnNodeAdded(newNode);
    }

    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        Count--;
        return true;
    }
    
    protected virtual void RemoveNode(TNode node)
    {
        if (node.Left == null)
        {
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            Transplant(node, node.Left);
        }
        else
        {
            TNode minNode = Minimum(node.Right);
            
            if (minNode.Parent != node)
            {
                Transplant(minNode, minNode.Right);
                minNode.Right = node.Right;
                minNode.Right!.Parent = minNode;
            }

            Transplant(node, minNode);
            minNode.Left = node.Left;
            minNode.Left!.Parent = minNode;
        }

        OnNodeRemoved(node.Parent, node.Parent?.Left ?? node.Parent?.Right);
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    protected void RotateLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y == null) {return;}

        x.Right = y.Left;
        if (y.Left != null) 
        {
            y.Left.Parent = x;
        }

        y.Parent = x.Parent;
        if (x.Parent == null)
        {
            
            Root = y;
        }
        else if (x.IsLeftChild)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }

        y.Left = x;
        x.Parent = y;
    }

    protected void RotateRight(TNode y)
    {
        TNode? x = y.Left;
        if (x == null) {return;}

        y.Left = x.Right;
        if (x.Right != null) 
        {
            x.Right.Parent = y;
        }

        x.Parent = y.Parent;
        if (y.Parent == null)
        {
            Root = x;
        }
        else if (y.IsLeftChild)
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }

        x.Right = y;
        y.Parent = x;
    }
    
    protected void RotateBigLeft(TNode x) => RotateDoubleLeft(x);
    
    protected void RotateBigRight(TNode y) => RotateDoubleRight(y);
    
    protected void RotateDoubleLeft(TNode x)
    {
        RotateRight(x.Right!);
        RotateLeft(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        RotateLeft(y.Left!);
        RotateRight(y);
    }

    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected TNode Minimum(TNode node)
    {
        while (node.Left!= null)
        {
            node = node.Left;
        }

        return node;
    }

    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }
    #endregion
    
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrder() => new TreeIterator(this, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrder() => new TreeIterator(this, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrder() => new TreeIterator(this, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrderReverse() => new TreeIterator(this, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrderReverse() => new TreeIterator(this, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrderReverse() => new TreeIterator(this, TraversalStrategy.PostOrderReverse);

    private struct TreeIterator : 
        IEnumerable<TreeEntry<TKey, TValue>>,
        IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly BinarySearchTreeBase<TKey, TValue, TNode> _tree;
        private readonly TraversalStrategy _strategy;
        private TNode? _current;
        private readonly Stack<TNode> _stack;
        private TNode? _lastVisited; 

        public TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        {
            _tree = tree;
            _strategy = strategy;
            _stack = new Stack<TNode>();
            _current = null;
            _lastVisited = null;
            Reset();
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;

        public TreeEntry<TKey, TValue> Current { get; private set; }
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return _strategy switch
            {
                TraversalStrategy.InOrder          => MoveNextInOrder(),
                TraversalStrategy.PreOrder         => MoveNextPreOrder(),
                TraversalStrategy.PostOrder        => MoveNextPostOrder(),
                TraversalStrategy.InOrderReverse   => MoveNextInOrderReverse(),
                TraversalStrategy.PreOrderReverse  => MoveNextPreOrderReverse(),
                TraversalStrategy.PostOrderReverse => MoveNextPostOrderReverse(),
                _ => false
            };
        }

        private bool MoveNextInOrder()
        {
            while (_current != null || _stack.Count > 0)
            {
                while (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Left;
                }

                _current = _stack.Pop();
                Current = new TreeEntry<TKey, TValue>(_current.Key, _current.Value, GetDepth(_current));
                _current = _current.Right;
                return true;
            }
            return false;
        }

        private bool MoveNextPreOrder()
        {
            if (_current == null && _stack.Count == 0) return false;

            if (_current == null)
                _current = _stack.Pop();

            Current = new TreeEntry<TKey, TValue>(_current.Key, _current.Value, GetDepth(_current));

            if (_current.Right != null) _stack.Push(_current.Right);
            if (_current.Left != null)  _stack.Push(_current.Left);

            _current = null;
            return true;
        }

        private bool MoveNextPostOrder()
        {
            while (_current != null || _stack.Count > 0)
            {
                if (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Left;
                }
                else
                {
                    TNode peek = _stack.Peek();

                    if (peek.Right != null && _lastVisited != peek.Right)
                    {
                        _current = peek.Right;
                    }
                    else
                    {
                        _lastVisited = _stack.Pop();
                        Current = new TreeEntry<TKey, TValue>(_lastVisited.Key, _lastVisited.Value, GetDepth(_lastVisited));
                        return true;
                    }
                }
            }
            return false;
        }

        private bool MoveNextInOrderReverse()
        {
            while (_current != null || _stack.Count > 0)
            {
                while (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Right;
                }

                _current = _stack.Pop();
                Current = new TreeEntry<TKey, TValue>(_current.Key, _current.Value, GetDepth(_current));
                _current = _current.Left;
                return true;
            }
            return false;
        }

        private bool MoveNextPreOrderReverse()
        {
            if (_current == null && _stack.Count == 0) return false;

            if (_current == null)
                _current = _stack.Pop();

            Current = new TreeEntry<TKey, TValue>(_current.Key, _current.Value, GetDepth(_current));

            if (_current.Left != null)  _stack.Push(_current.Left);
            if (_current.Right != null) _stack.Push(_current.Right);

            _current = null;
            return true;
        }

        private bool MoveNextPostOrderReverse()
        {
            while (_current != null || _stack.Count > 0)
            {
                if (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Right;   
                }
                else
                {
                    TNode peek = _stack.Peek();

                    if (peek.Left != null && _lastVisited != peek.Left)
                    {
                        _current = peek.Left;
                    }
                    else
                    {
                        _lastVisited = _stack.Pop();
                        Current = new TreeEntry<TKey, TValue>(_lastVisited.Key, _lastVisited.Value, GetDepth(_lastVisited));
                        return true;
                    }
                }
            }
            return false;
        }

        private int GetDepth(TNode node)
        {
            int depth = 0;
            TNode? curr = node;
            while (curr?.Parent != null)
            {
                depth++;
                curr = curr.Parent;
            }
            return depth;
        }

        public void Reset()
        {
            _stack.Clear();
            _current = _tree.Root;
            _lastVisited = null;
            Current = default;
        }

        public void Dispose() { }
    }
    
    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var entry in InOrder())
        {
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}