using System;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.AllocatorManager;
namespace RBTree
{
    public struct IntCompare : IComparableIn<int>
    {
        public int CompareTo(in int left, in int right)
        {
            return left.CompareTo(right);
        }
    }
    public enum Color { Red, Black }
    public interface IComparableIn<T>
    {
        int CompareTo(in T left, in T right);
    }
    public struct TreeNode<T> : IEquatable<TreeNode<T>> where T : unmanaged
    {
        public T NodeData;
        public int Index;
        public int Right;
        public int Left;
        public int Parent;
        public Color Color;

        public TreeNode(T nodeData)
        {
            Index = 0;
            Right = 0;
            Left = 0;
            Parent = 0;
            Color = Color.Red;
            this.NodeData = nodeData;
        }
        public readonly static TreeNode<T> Null = new TreeNode<T>() { Color = Color.Black, Left = 0, Right = 0, Index = 0, Parent = 0, NodeData = default };

        public readonly bool IsNullRight => Right <= 0;
        public readonly bool IsNullLeft => Left <= 0;
        public readonly bool IsNullParent => Parent <= 0;
        public readonly bool IsNull => Index <= 0;
        public readonly int KeyCompareTo<TCompareTo>(in TreeNode<T> other) where TCompareTo : unmanaged, IComparableIn<T>
        {
            TCompareTo d = default;
            return d.CompareTo(in NodeData, in other.NodeData);
        }
        public readonly bool Equals(TreeNode<T> other)
        {
            return Index == other.Index
                && Right == other.Right
                && Left == other.Left
                && Parent == other.Parent;

        }
        public override int GetHashCode()
        {
            return Index;
        }

        public readonly bool Equals(in TreeNode<T> other)
        {
            return Index == other.Index
                && Right == other.Right
                && Left == other.Left
                && Parent == other.Parent;
        }
        public readonly bool ReferenceEquals(in TreeNode<T> other)
        {
            return Index == other.Index;
        }


        public static bool operator ==(in TreeNode<T> left, in TreeNode<T> right)
        {
            return left.Equals(in right);
        }
        public static bool operator !=(in TreeNode<T> left, in TreeNode<T> right)
        {
            return !left.Equals(in right);
        }

        public override bool Equals(object obj)
        {
            return this.Equals((TreeNode<T>)obj);
        }
    }
    public struct RBTreeLeft<T, TCompareTo> : IDisposable where T : unmanaged, IEquatable<T> where TCompareTo : unmanaged, IComparableIn<T>
    {
        private int Root;
        private UnsafeList<TreeNode<T>> List; //存储树节点的列表
        private UnsafeList<int> IndexQueue; //存储空闲索引的队列
        private UnsafeList<int> sortList; //存储排序后的索引列表
        private bool isDirtySort; //是否需要重新排序
        private UnsafeHashMap<T, int> Map; //存储节点数据到索引的映射
        private TCompareTo CompareTo;
        private int Count;
        public int Length => Count - 1;
        public RBTreeLeft(int buffer, AllocatorHandle allocatorHandle)
        {
            isDirtySort = true;
            List = new UnsafeList<TreeNode<T>>(buffer, allocatorHandle);
            List.Length = buffer;
            sortList = new UnsafeList<int>(buffer, allocatorHandle);
            sortList.Length = buffer;
            Count = 0;
            IndexQueue = new UnsafeList<int>(buffer, allocatorHandle);
            Map = new UnsafeHashMap<T, int>(buffer, allocatorHandle);
            Root = 0;
            CompareTo = default;
            AddToCollection(TreeNode<T>.Null);
        }
        public const int NullIndex = 0;  //0索引作为null值存储,请小心处理
        public ref TreeNode<T> this[int index]
        {
            get
            {
                return ref List.ElementAt(index);
                //return  ref new Span<TreeNode<T>>(List)[index];
            }
        }

        public ref TreeNode<T> CreateTreeNode(in T nodeData)
        {
            var Index = NextIndex();
            AddToCollection(new TreeNode<T>(nodeData)
            {
                Color = Color.Black,
                Index = Index,
            });
            return ref this[Index];
        }

        public int NextIndex()
        {
            int result;
            if (IndexQueue.Length > 0)
            {
                result = IndexQueue[^1];
                IndexQueue.RemoveAt(IndexQueue.Length - 1);
                return result;
            }
            else
            {
                return Count;
            }
        }
        private void AddToCollection(TreeNode<T> treeNode)
        {
            if (List.Length <= treeNode.Index)
            {
                List.Resize(treeNode.Index * 2);
                sortList.Resize(treeNode.Index * 2);
            }
            List[treeNode.Index] = treeNode;
            Map.Add(treeNode.NodeData, Count);
            Count++;
        }
        private void RemoveFromCollection(int treeNode)
        {
            IndexQueue.Add(Map[this[treeNode].NodeData]);
            Map.Remove(this[treeNode].NodeData);
        }

        private void Delete(int index)
        {
            ReplaceChild(this[index].Parent, index, NullIndex);
            RemoveFromCollection(index);
            this[index] = this[NullIndex];
        }
        public T Add( T nodeData)
        {
            isDirtySort = true;
            Internal_Add(nodeData);
            return nodeData;
        }
        private int CompareIn(int index, in T nodeData)
        {
            return CompareTo.CompareTo(in this[index].NodeData, in nodeData);
        }
        
        private void Internal_Add(in T nodeData)
        {
            if (CompareIn(NullIndex, in nodeData) == 0)
                return;

            ref var newTreeNode = ref CreateTreeNode(in nodeData);
            if (this[Root].IsNull)
            {
                newTreeNode.Color = Color.Black;
                Root = newTreeNode.Index;
                return;
            }

            newTreeNode.Color = Color.Red;
            var availableNodeIndex = NullIndex;
            var currentIndex = Root;
            while (currentIndex != NullIndex)
            {
                availableNodeIndex = currentIndex;

                currentIndex = CompareIn(currentIndex, in nodeData) > 0 ? this[currentIndex].Left : this[currentIndex].Right;
            }
            newTreeNode.Parent = availableNodeIndex;
            if (CompareIn(availableNodeIndex, in nodeData) > 0)
            {
                this[availableNodeIndex].Left = newTreeNode.Index;
            }
            else
            {
                this[availableNodeIndex].Right = newTreeNode.Index;
            }
            FixAdding(newTreeNode.Index);
        }
        private void FixAdding(int treeNodeIndex)
        {


            int treeNodeIndexTemp = treeNodeIndex;
            if (treeNodeIndexTemp == Root)
            {
                this[treeNodeIndexTemp].Color = Color.Black;
                return;
            }
            while ((this[treeNodeIndexTemp].IsNullParent ? false : this[this[treeNodeIndexTemp].Parent].Color == Color.Red))
            {
                int grandFather = this[this[treeNodeIndexTemp].Parent].Parent;
                if (grandFather == NullIndex)
                {
                    break;
                }
                var uncle = this[grandFather].Left == this[treeNodeIndexTemp].Parent ? this[grandFather].Right : this[grandFather].Left;
                if (uncle != NullIndex && this[uncle].Color == Color.Red)
                {
                    RecoloringFix(treeNodeIndexTemp, uncle, grandFather);
                    treeNodeIndexTemp = grandFather;
                }
                else
                {
                    treeNodeIndexTemp = this[RotationFix(treeNodeIndexTemp, grandFather)].Index;
                }
            }
            this[Root].Color = Color.Black;
        }

        public enum To
        {
            Left,
            Right,
            LeftUp,
            RightUp
        }
        //exclude 0 index
        public ReadOnlySpan<int> InOrderSpanIndex()
        {
            if (!isDirtySort)
            {
                unsafe
                {
                    return new ReadOnlySpan<int>(sortList.Ptr, Map.Count - 1);
                }
                //return new Span<int>(sortList.AsSpan,0,Map.Count);
            }
            if (Root == NullIndex)
            {
                return new ReadOnlySpan<int>();
            }
            isDirtySort = false;
            var node = Root;
            var want = To.Left;
            var count = 0;
            bool loop = true;
            while (loop)
            {
                switch (want)
                {
                    case To.Left:
                        if (this[node].IsNullLeft) { want = To.Right; sortList[count] = node; count++; } else { node = this[node].Left; }
                        continue;
                    case To.Right:
                        if (this[node].IsNullRight) { want = To.LeftUp; } else { want = To.Left; node = this[node].Right; }
                        continue;
                    case To.LeftUp:
                        if (this[this[node].Parent].Right == node)
                        {
                            node = this[node].Parent;
                        }
                        else
                        {
                            want = To.RightUp;
                        }
                        continue;
                    case To.RightUp:
                        if (this[this[node].Parent].Left == node)
                        {
                            node = this[node].Parent;
                            sortList[count] = node;
                            count++;
                            want = To.Right;
                        }
                        else
                        {
                            loop = false;
                        }
                        continue;
                }
            }
            //return sortList.AsReadOnlySpan().Slice(0, count);
            unsafe { return new ReadOnlySpan<int>(sortList.Ptr, count); }
        }

        /// <summary>
        /// ensure tree length > 0
        /// </summary>
        public ReadOnlySpan<TreeNode<T>> Values {
            get {
 
                unsafe {
                    return new ReadOnlySpan<TreeNode<T>>(List.Ptr + 1, Map.Count - 1);
                }
            }
        }

        public void Print(object obj)
        {
            UnityEngine.Debug.Log(obj);
        }
        public bool Contains(in T key)
        {
            return Map.ContainsKey(key);
        }

        public bool DeleteMap(in T key, out T value)
        {
            value = default;
            if (!Map.TryGetValue(key, out int treeNodeIndex))
            {
                return false;
            }
            isDirtySort = true;
            var countChildren = GetCountChildren(treeNodeIndex);
            var delectIndex = countChildren switch
            {
                0 => ZeroChild(treeNodeIndex),
                1 => OneChild(treeNodeIndex),
                2 => TwoChild(treeNodeIndex),
                _ => throw new Exception()
            };
            bool isFix = true;
            if (delectIndex == Root
                || this[delectIndex].Color == Color.Red
                || this[delectIndex].IsNull)
            {
                isFix = false;
            }

            if (isFix)
            {
                FixDelete(delectIndex);
            }
            value = this[treeNodeIndex].NodeData;
            Delete(delectIndex);
            //RemoveFromCollection(delectIndex);
            return true;
        }

        public bool Delete(in T key)
        {
            return DeleteMap(in key, out T value);
        }
        private void SwapTreeNodeKey(int index, int other)
        {
            var nd = this[index].NodeData;
            this[index].NodeData = this[other].NodeData;
            this[other].NodeData = nd;
            Map[nd] = other;
            Map[this[index].NodeData] = index;

        }

        private void SwapColor(int a, int b)
        {
            Color color = this[a].Color;
            this[a].Color = this[b].Color;
            this[b].Color = color;
        }


        public int MinFromNode(int start)
        {
            start = start <= 0 ? 0 : start;
            if (start != NullIndex)
            {
                while (!this[start].IsNullLeft)
                {
                    start = this[start].Left;
                }
            }
            return start;
        }

        public int MaxFromNode(int start)
        {
            start = start <= 0 ? 0 : start;
            if (start != NullIndex)
            {
                while (!this[start].IsNullRight)
                {
                    start = this[start].Right;
                }
            }
            return start;
        }
        public void PrintTree()
        {
            PrintSubTree(Root);
        }
        private void PrintLeft(int index)
        {
            if (!this[index].IsNullLeft)
            {
                PrintLeft(index);
            }
            PrintIndex(index);
            PrintRight(index);
        }

        public void PrintTreeNodeRoot()
        {
            PrintTreeNode(new StringBuilder(), Root, true);
        }
        public void PrintTreeNode(StringBuilder indent, int index, bool isTail)
        {
            if (this[index].Right != NullIndex)
            {
                PrintTreeNode(new StringBuilder()
                             .Append(indent)
                             .Append(isTail ? "│   " : "    "), this[index].Right, false);
            }

            //inde(indent);
            indent.Append(isTail ? "└── " : "┌── ");

            if (this[index].Color == Color.Red)
            {
                indent.Append(this[index].NodeData + "r" + Environment.NewLine);
            }
            else
            {
                indent.AppendLine(this[index].NodeData + "b");
                //Print(Environment.NewLine);
            } // print current node

            if (this[index].Left != NullIndex)
            {
                PrintTreeNode(new StringBuilder()
                             .Append(indent)
                             .Append(isTail ? "    " : "│   "), this[index].Left, true);

            }
            Print(index);
        }
        //{
        //    if (this[index].IsNullParent)
        //    {
        //        Debug.Log(this[index].)
        //    }
        //}
        private void PrintIndex(int index)
        {
            Print(this[index].NodeData.ToString());
        }
        private void PrintRight(int index)
        {
            if (!this[index].IsNullRight)
            {
                PrintLeft(this[index].Right);
            }
            //PrintParent(index);
        }
        public void PrintSubTree(int index)
        {
            PrintLeft(index);
        }

        private void RecoloringFix(int node, int uncle, int grandFather)
        {
            this[this[node].Parent].Color = Color.Black;
            this[uncle].Color = Color.Black;
            this[grandFather].Color = Color.Red;
        }

        private int RotationFix(int treeNodeIndex, int grandFatherIndex)
        {
            if (this[treeNodeIndex].Parent == this[grandFatherIndex].Left && treeNodeIndex == this[this[treeNodeIndex].Parent].Right)
            {
                LeftRotation(this[treeNodeIndex].Parent);
                treeNodeIndex = this[treeNodeIndex].Left;
            }
            else if (this[treeNodeIndex].Parent == this[grandFatherIndex].Right && treeNodeIndex == this[this[treeNodeIndex].Parent].Left)
            {
                RightRotation(this[treeNodeIndex].Parent);
                treeNodeIndex = this[treeNodeIndex].Right;
            }
            this[grandFatherIndex].Color = Color.Red;
            this[this[treeNodeIndex].Parent].Color = Color.Black;


            if (this[this[treeNodeIndex].Parent].Left == treeNodeIndex)
            {
                RightRotation(grandFatherIndex);
            }
            else
            {
                LeftRotation(grandFatherIndex);
            }
            return grandFatherIndex;
        }
        private void FixDelete(int startIndex)
        {
            while (!this[startIndex].IsNull && startIndex != Root)
            {
                var parent = this[startIndex].Parent;
                var left = this[parent].Left;
                var right = this[parent].Right;
                var brother = startIndex == left ? right : left;
                if (this[brother].Color == Color.Black)
                {
                    var brotherChildLeft = this[brother].Left;
                    var brotherChildRight = this[brother].Right;
                    if (this[brotherChildLeft].Color == Color.Black
                    && this[brotherChildRight].Color == Color.Black)
                    {
                        if (this[parent].Color == Color.Red)
                        {
                            SwapColor(parent, brother);
                            break;
                        }
                        else
                        {
                            this[brother].Color = Color.Red;
                            startIndex = parent;
                            continue;
                        }

                    }
                    if (brother == left)
                    {
                        if (this[brotherChildLeft].Color == Color.Red)
                        {
                            SwapColor(parent, brother);
                            this[brotherChildLeft].Color = Color.Black;
                            RightRotation(parent);
                            break;
                        }
                        else
                        {
                            SwapColor(brother, brotherChildRight);
                            RightRotation(brother);
                            continue;
                        }
                    }
                    else
                    {
                        if (this[brotherChildRight].Color == Color.Red)
                        {
                            SwapColor(parent, brother);
                            this[brotherChildRight].Color = Color.Black;
                            LeftRotation(parent);
                            break;
                        }
                        else
                        {
                            SwapColor(brother, brotherChildLeft);
                            LeftRotation(brother);
                            continue;
                        }
                    }
                }
                else
                {
                    SwapColor(parent, brother);
                    continue;
                }
                var grandFather = this[parent].Parent;
                var uncle = this[grandFather].Right == parent ? this[grandFather].Left : this[grandFather].Right;
                //var brotherChildLeft = this[brother].Left;
                //var brotherChildRight = this[brother].Right;
            }

        }

        //后置查找
        //删除节点总是找树叶中,来代替删除节点
        public int FindDeleteTreeNode(int treeNodeIndex)
        {
            ref var treeNode = ref this[treeNodeIndex];
            if (treeNode.IsNull)
            {
                return NullIndex;
            }
            ref var right = ref this[treeNode.Right];
            var nodeIndex = 0;
            if (right.IsNull)
            {
                nodeIndex = treeNode.Parent;
                while (CompareIn(nodeIndex, in treeNode.NodeData) < 0)
                {
                    nodeIndex = this[nodeIndex].Parent;
                }
                return nodeIndex;
            }

            while (!right.IsNull)
            {
                nodeIndex = right.Index;
                right = ref this[right.Left];
            }
            return nodeIndex;
        }
        //返回需要删除的node
        private int ZeroChild(int nodeIndex)
        {
            return nodeIndex;
        }

        private int OneChild(int nodeIndex)
        {
            var child = this[nodeIndex].IsNullLeft ? this[nodeIndex].Right : this[nodeIndex].Left;
            SwapTreeNodeKey(nodeIndex, child);
            return child;
        }
        private int TwoChild(int nodeIndex)
        {
            var deleteIndex = FindDeleteTreeNode(nodeIndex);
            SwapTreeNodeKey(nodeIndex, deleteIndex);
            var childCount = GetCountChildren(deleteIndex);
            return childCount == 0 ? ZeroChild(deleteIndex) : OneChild(deleteIndex);
        }

        private void ReplaceChild(int parentIndex, int treeNodeIndex, int newIndex)
        {
            if (parentIndex != NullIndex)
            {
                if (this[parentIndex].Left == treeNodeIndex)
                {
                    this[parentIndex].Left = newIndex;
                }
                else
                {
                    this[parentIndex].Right = newIndex;
                }
            }
        }

        private bool IndexNull(int index) => index <= 0;

        private int GetCountChildren(int treeNodeIndex)
        {
            ref var treeNode = ref this[treeNodeIndex];
            int childerCount = 0;
            if (!treeNode.IsNullLeft)
                childerCount++;
            if (!treeNode.IsNullRight)
                childerCount++;
            return childerCount;
        }

        private void LeftRotation(int treeNodeIndex)
        {
            var right = this[treeNodeIndex].Right;
            var rightLeft = this[right].Left;
            this[right].Parent = this[treeNodeIndex].Parent;
            ReplaceChild(this[treeNodeIndex].Parent, treeNodeIndex, right);
            if (Root == treeNodeIndex)
            {
                Root = this[right].Index;
            }
            this[treeNodeIndex].Parent = right;
            this[treeNodeIndex].Right = rightLeft;
            this[right].Left = treeNodeIndex;
            if (!IndexNull(rightLeft))
            {
                this[rightLeft].Parent = treeNodeIndex;
            }

        }

        private void RightRotation(int treeNodeIndex)
        {
            var left = this[treeNodeIndex].Left;
            var leftRight = this[left].Right;
            this[left].Parent = this[treeNodeIndex].Parent;
            ReplaceChild(this[treeNodeIndex].Parent, treeNodeIndex, left);
            if (Root == treeNodeIndex)
            {
                Root = this[left].Index;
            }
            this[treeNodeIndex].Parent = left;
            this[treeNodeIndex].Left = leftRight;
            this[left].Right = treeNodeIndex;
            if (!IndexNull(leftRight))
            {
                this[leftRight].Parent = treeNodeIndex;
            }

        }

        public void Clear()
        {
            isDirtySort = true;
            var buffer = List.Length;
            List.Clear();
            List.Length = buffer;
            buffer = sortList.Length;
            sortList.Clear();
            sortList.Length = buffer;
            Count = 0;
            buffer = IndexQueue.Length;
            IndexQueue.Clear();
            IndexQueue.Length = buffer;
            Map.Clear();
            Root = 0;
            AddToCollection(TreeNode<T>.Null);
        }
        public void Dispose()
        {
            List.Dispose();
            Map.Dispose();
            IndexQueue.Dispose();
            sortList.Dispose();
        }
    }
}