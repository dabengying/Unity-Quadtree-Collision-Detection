﻿/*
 *  增加了半径的四叉树，同样有配合使用的 QuadtreeWithRadiusObject 和 QuadtreeWithRadiusCollider
 *  
 *  增加了半径后存入、检测、移除的步骤都有所改变
 *  
 *  先从检测开始：
 *      在第零步里面已经写过如何判断，原理是自上到下找到树梢之后计算距离和检测半径。
 *      在增加了碰撞器半径后向下找到树梢的步骤出现了变化：
 *          第零步的叶子没有半径，不管叶子在节点区域里的哪个位置，叶子的碰撞范围都不会超出节点范围。
 *          这一步的叶子有了半径，如果叶子的位置比较靠近节点区域边缘，这个叶子的碰撞范围就会超出节点区域。
 *          这样向下检测一个子节点有没有可能发生碰撞时就需要考虑到这个子节点区域里半径最大的那个节点可能已经处于边缘处。
 *          当然我们可以通过遍历节点里所有的叶子得出最大半径，但这样暴力解决计算量比不用四叉树还大。
 *          为了减少计算量我们可以给每个节点存一个最大半径，这样就只需要查看一次最大半径就可以判断出结果。
 *          也就是说需要增加最大半径。
 *      
 *      之后看存入：
 *          第零步光存入就算完，但增加了最大半径这个值后就不一样了，需要每次存入的时候都更新最大半径。
 *          光是自身更新还不够，还要向上更新，否则会出现这样一种情况：
 *              在左上角有一个树梢里有一个叶子半径巨大无比，碰撞区域比整个四叉树的区域还要大。
 *              测试点在右下角。
 *              在根节点向下找的时候法线左上方的子节点的最大半径很小，以为他不会发生碰撞，没有向下寻找。
 *              这样这个不管检测哪都会碰到的叶子就因为有一节数值的最大半径太小而被忽略掉了。
 *              为了应对这种情况必须要向上更新最大半径。
 *              
 *      最后是移除
 *          第零步的移除也是移除就算完了，这一步的移除则要计算新的最大半径。
 *          当然也要向上更新，不然最大的叶子已经没了还计算过来会浪费计算量。
 *      
 *  附带增加了存入和移除叶子的是否成功的bool返回
 */
 
using System.Collections.Generic;
using UnityEngine;

public class QuadtreeWithRadiusLeaf<T>
{
    public T obj
    {
        get { return _obj; }
    }
    T _obj;

    public Vector2 position
    {
        get { return _position; }
        set { _position = value; }
    }
    Vector2 _position;

    public float radius
    {
        get { return _radius; }
        set { _radius = value; }
    }
    float _radius;


    public QuadtreeWithRadiusLeaf(T obj, Vector2 position, float radius)
    {
        _obj = obj;
        _position = position;
        _radius = radius;
    }
}

public class QuadtreeWithRadius<T>
{
    Rect _rect;

    float _maxRadius = float.MinValue;
    /*
     *  这个值代表着这个节点里半径最大的那个叶子的半径，节点里没有叶子时设为0或者任何负数都不影响正确性，设为 float 的最小值的原因如下：
     *  
     *  由于这个四叉树在第零步的基础上增加了叶子的半径，碰撞检测向下递归时判断子节点有没有可能有叶子发生碰撞就变得复杂了：
     *  一个节点里可以有好多种半径的叶子，每个叶子可能在任意位置，因此要判断一个子节点里有没有叶子可能发生碰撞的方法变成了 计算检测点到子节点区域的距离，如果这个距离小于测试半径和子节点最大半径的叶子的半径，则说明这个子节点里有可能有叶子会碰撞到。
     *  
     *  根据这个方法，如果一个子节点的最大半径是负数，则判断会发生有趣的现象：检测范围已经覆盖到了这个节点的范围，但因为节点半径是负数，判断上要减去这个值。
     *  那么如果一个子节点的最大半径是float最小值，那么即使测试半径是float的最大值，这个节点也不会被判断为可能发生碰撞。
     *  如果将没有叶子的节点的最大半径设为float最小值，则碰撞检测时这个节点判断为不会发生碰撞，就不会向下迭代这个节点，可以节省一点计算量。
     */

    QuadtreeWithRadius<T> _parent;
    /*
     *  父节点，用来向上更新最大半径
     */

    QuadtreeWithRadius<T> _upperRightChild;
    QuadtreeWithRadius<T> _lowerRightChild;
    QuadtreeWithRadius<T> _lowerLeftChild;
    QuadtreeWithRadius<T> _upperLeftChild;

    List<QuadtreeWithRadiusLeaf<T>> _leafs = new List<QuadtreeWithRadiusLeaf<T>>();

    int _maxLeafNumber;
    float _minWidth;
    float _minHeight;


    public QuadtreeWithRadius(float x, float y, float width, float height, int maxLeafNumber, float minWidth, float minHeight, QuadtreeWithRadius<T> parent = null)
    {
        _rect = new Rect(x, y, width, height);

        _maxLeafNumber = maxLeafNumber;
        _minWidth = minWidth;
        _minHeight = minHeight;

        _parent = parent;
    }


    /*
     *  存入叶子
     *  在第零步的基础上增加了更新半径功能
     */
    public bool SetLeaf(QuadtreeWithRadiusLeaf<T> leaf)
    {
        if (DontHaveChildren())
        {
            _leafs.Add(leaf);

            if (leaf.radius > _maxRadius)
            {
                _maxRadius = leaf.radius;

                //向上更新最大半径
            }

            return true;
        }
        else
        {
            return SetLeafToChildren(leaf);
        }
    }
    bool DontHaveChildren()
    {
        return _upperRightChild == null || _lowerRightChild == null || _lowerLeftChild == null || _upperLeftChild == null;      //四个子节点是一起创建的，原理上说一个不存在另外三个也不存在，但假设只有一个不存在插入的叶子又在这个位置就要出事了
    }
    bool SetLeafToChildren(QuadtreeWithRadiusLeaf<T> leaf)
    {
        if (_upperRightChild._rect.PointToRectDistance(leaf.position) == 0)
            return _upperRightChild.SetLeaf(leaf);
        if (_lowerRightChild._rect.PointToRectDistance(leaf.position) == 0)
            return _lowerRightChild.SetLeaf(leaf);
        if (_lowerLeftChild._rect.PointToRectDistance(leaf.position) == 0)
            return _lowerLeftChild.SetLeaf(leaf);
        if (_upperLeftChild._rect.PointToRectDistance(leaf.position) == 0)
            return _upperLeftChild.SetLeaf(leaf);

        Debug.LogError("向位置在" + _rect.position + "宽高是" + _rect.size + "的节点存入叶子时发生错误：叶子不在所有子节点的范围里。");   //Debug.LogError：在Console面板输出Error，就是红色那种消息
        return false;
    }

    /*
     *  半径更新
     *  树梢遍历叶子更新
     *  树梢检测最大值更新
     *  
     *  树枝检测子节点更新
     */
    void UpwardUpdateMaxRadius()
    {
        float newManRaiuds = GetChildrenMaxRadius();
        if (newManRaiuds != _maxRadius)
        {
            _maxRadius = newManRaiuds;

            if (_parent != null)
            {
                _parent.UpwardUpdateMaxRadius();
            }
        }
    }

    float GetChildrenMaxRadius()
    {
        return Mathf.Max(_upperRightChild._maxRadius, _lowerRightChild._maxRadius, _lowerLeftChild._maxRadius, _upperLeftChild._maxRadius);
    }
}