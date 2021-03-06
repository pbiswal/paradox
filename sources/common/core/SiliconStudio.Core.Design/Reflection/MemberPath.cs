﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SiliconStudio.Core.Reflection
{
    /// <summary>
    /// Allows to get/set a property/field value on a deeply nested object instance (supporting 
    /// members, list access and dictionary access)
    /// </summary>
    public sealed class MemberPath
    {
        /// <summary>
        /// We use a thread local static to avoid allocating a list of reference objects every time we access a property
        /// </summary>
        [ThreadStatic] private static List<object> stackTLS;

        private readonly List<MemberPathItem> items;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberPath"/> class.
        /// </summary>
        public MemberPath() : this(16)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberPath"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public MemberPath(int capacity)
        {
            items = new List<MemberPathItem>(capacity);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberPath"/> class.
        /// </summary>
        /// <param name="items">The items.</param>
        private MemberPath(List<MemberPathItem> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            this.items = new List<MemberPathItem>(items.Capacity);
            this.items.AddRange(items);
        }

        /// <summary>
        /// Ensures the capacity of the paths definition when using <see cref="Push(SiliconStudio.Core.Reflection.IMemberDescriptor)"/> methods.
        /// </summary>
        /// <param name="pathCount">The path count.</param>
        public void EnsureCapacity(int pathCount)
        {
            items.Capacity = pathCount;
        }

        /// <summary>
        /// Clears the current path.
        /// </summary>
        public void Clear()
        {
            items.Clear();
        }

        /// <summary>
        /// Gets the custom attribute of the last property/field from this member path.
        /// </summary>
        /// <typeparam name="T">Type of the attribute</typeparam>
        /// <returns>A custom attribute or null if not found</returns>
        public T GetCustomAttribute<T>() where T : Attribute
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            for(int i = items.Count - 1; i >= 0; i--)
            {
                var descriptor = items[i].MemberDescriptor as MemberDescriptorBase;
                if (descriptor == null)
                {
                    continue;
                }
                var attributes = descriptor.MemberInfo.GetCustomAttributes(typeof(T), false);
                if (attributes.Length > 0)
                {
                    return (T)attributes[0];
                }

                break;
            }
            return null;
        }

        /// <summary>
        /// Pushes a member access on the path.
        /// </summary>
        /// <param name="descriptor">The descriptor of the member.</param>
        /// <exception cref="System.ArgumentNullException">descriptor</exception>
        public void Push(IMemberDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            AddItem(descriptor is FieldDescriptor ? (MemberPathItem)new FieldPathItem((FieldDescriptor)descriptor) : new PropertyPathItem((PropertyDescriptor)descriptor));
        }

        /// <summary>
        /// Pushes an array access on the path.
        /// </summary>
        /// <param name="descriptor">The descriptor of the array.</param>
        /// <param name="index">The index in the array.</param>
        /// <exception cref="System.ArgumentNullException">descriptor</exception>
        public void Push(ArrayDescriptor descriptor, int index)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            AddItem(new ArrayPathItem(index));
        }

        /// <summary>
        /// Pushes an collection access on the path.
        /// </summary>
        /// <param name="descriptor">The descriptor of the collection.</param>
        /// <param name="index">The index in the collection.</param>
        /// <exception cref="System.ArgumentNullException">descriptor</exception>
        public void Push(CollectionDescriptor descriptor, int index)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            AddItem(new CollectionPathItem(descriptor, index));
        }

        /// <summary>
        /// Pushes an dictionary access on the path.
        /// </summary>
        /// <param name="descriptor">The descriptor of the dictionary.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">descriptor</exception>
        public void Push(DictionaryDescriptor descriptor, object key)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            AddItem(new DictionaryPathItem(descriptor, key));
        }

        /// <summary>
        /// Pops the last item from the current path.
        /// </summary>
        public void Pop()
        {
            if (items.Count > 0)
            {
                items.RemoveAt(items.Count - 1);
            }
        }

        public bool Apply(object rootObject, MemberPathAction actionType, object value)
        {
            if (rootObject == null) throw new ArgumentNullException("rootObject");
            if (rootObject.GetType().IsValueType) throw new ArgumentException("Value type for root objects are not supported", "rootObject");
            if (actionType != MemberPathAction.ValueSet && actionType != MemberPathAction.CollectionAdd && value != null)
            {
                throw new ArgumentException("Value must be null for action != (MemberActionType.SetValue || MemberPathAction.CollectionAdd)");
            }

            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("This instance doesn't contain any path. Use Push() methods to populate paths");
            }

            var lastItem = items[items.Count - 1];
            switch (actionType)
            {
                case MemberPathAction.CollectionAdd:
                    if (!(lastItem is CollectionPathItem))
                    {
                        throw new ArgumentException("Invalid path [{0}] for action [{1}]. Expecting last path to be a collection item".ToFormat(this, actionType));
                    }
                    break;
                case MemberPathAction.CollectionRemove:
                    if (!(lastItem is CollectionPathItem) && !(lastItem is ArrayPathItem))
                    {
                        throw new ArgumentException("Invalid path [{0}] for action [{1}]. Expecting last path to be a collection/array item".ToFormat(this, actionType));
                    }
                    break;

                case MemberPathAction.DictionaryRemove:
                    if (!(lastItem is DictionaryPathItem))
                    {
                        throw new ArgumentException("Invalid path [{0}] for action [{1}]. Expecting last path to be a dictionary item".ToFormat(this, actionType));
                    }
                    break;
            }

            var stack = stackTLS;
            try
            {
                object nextObject = rootObject;

                if (stack == null)
                {
                    stack = new List<object>();
                    stackTLS = stack;
                }
                else
                {
                    stack.Clear();
                }

                stack.Add(nextObject);
                for (int i = 0; i < items.Count - 1; i++)
                {
                    var item = items[i];
                    nextObject = item.GetValue(nextObject);
                    stack.Add(nextObject);
                }

                switch (actionType)
                {
                    case MemberPathAction.ValueSet:
                        lastItem.SetValue(stack, stack.Count - 1, nextObject, value);
                        break;

                    case MemberPathAction.DictionaryRemove:
                        ((DictionaryPathItem)lastItem).Descriptor.Remove(nextObject, ((DictionaryPathItem)lastItem).Key);
                        break;

                    case MemberPathAction.CollectionAdd:
                        ((CollectionPathItem)lastItem).Descriptor.Add(nextObject, value);
                        break;

                    case MemberPathAction.CollectionRemove:
                        ((CollectionPathItem)lastItem).Descriptor.RemoveAt(nextObject, ((CollectionPathItem)lastItem).Index);
                        break;
                }
            }
            catch (Exception)
            {
                // If an exception occured, we cannot resolve this member path to a valid property/field
                return false;
            }
            finally
            {
                if (stack != null)
                {
                    stack.Clear();
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the value from the specified root object following this instance path.
        /// </summary>
        /// <param name="rootObject">The root object.</param>
        /// <param name="value">The returned value.</param>
        /// <returns><c>true</c> if evaluation of the path succeeded and the value is valid, <c>false</c> otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">rootObject</exception>
        public bool TryGetValue(object rootObject, out object value)
        {
            if (rootObject == null) throw new ArgumentNullException("rootObject");
            value = null;
            try
            {
                object nextObject = rootObject;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    nextObject = item.GetValue(nextObject);
                }
                value = nextObject;
            }
            catch (Exception)
            {
                // If an exception occured, we cannot resolve this member path to a valid property/field
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the value from the specified root object following this instance path.
        /// </summary>
        /// <param name="rootObject">The root object.</param>
        /// <param name="value">The returned value.</param>
        /// <param name="overrideType">Type of the override.</param>
        /// <returns><c>true</c> if evaluation of the path succeeded and the value is valid, <c>false</c> otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">rootObject</exception>
        public bool TryGetValue(object rootObject, out object value, out OverrideType overrideType)
        {
            if (rootObject == null) throw new ArgumentNullException("rootObject");
            if (items.Count == 0) throw new InvalidOperationException("No items pushed via Push methods");

            value = null;
            overrideType = OverrideType.Base;
            try
            {
                object nextObject = rootObject;

                var lastItem = items[items.Count - 1];
                var memberDescriptor = lastItem.MemberDescriptor;

                for (int i = 0; i < items.Count - 1; i++)
                {
                    var item = items[i];
                    nextObject = item.GetValue(nextObject);
                }

                overrideType = nextObject.GetOverride(memberDescriptor);
                value = lastItem.GetValue(nextObject);

            }
            catch (Exception)
            {
                // If an exception occured, we cannot resolve this member path to a valid property/field
                return false;
            }
            return true;
        }

        /// <summary>
        /// Clones this instance, cloning the current path.
        /// </summary>
        /// <returns>A clone of this instance.</returns>
        public MemberPath Clone()
        {
            return new MemberPath(items);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            var text = new StringBuilder();
            bool isFirst = true;
            foreach (var memberPathItem in items)
            {
                text.Append(memberPathItem.GetName(isFirst));
                isFirst = false;
            }
            return text.ToString();
        }

        private void AddItem(MemberPathItem item)
        {
            var previousItem = items.Count > 0 ? items[items.Count - 1] : null;
            items.Add(item);
            item.Parent = previousItem;
        }

        private abstract class MemberPathItem
        {
            public MemberPathItem Parent { get; set; }

            public abstract IMemberDescriptor MemberDescriptor { get; }

            public abstract object GetValue(object thisObj);

            public abstract void SetValue(List<object> stack, int objectIndex, object thisObject, object value);

            public abstract string GetName(bool isFirst);
        }

        private sealed class PropertyPathItem : MemberPathItem
        {
            private readonly PropertyDescriptor Descriptor;

            private readonly bool isValueType;

            public PropertyPathItem(PropertyDescriptor descriptor)
            {
                this.Descriptor = descriptor;
                isValueType = descriptor.DeclaringType.IsValueType;
            }

            public override IMemberDescriptor MemberDescriptor
            {
                get
                {
                    return Descriptor;
                }
            }

            public override object GetValue(object thisObj)
            {
                return Descriptor.Get(thisObj);
            }

            public override void SetValue(List<object> stack, int objectIndex, object thisObject, object value)
            {
                Descriptor.Set(thisObject, value);

                if (isValueType && Parent != null)
                {
                    Parent.SetValue(stack, objectIndex - 1, stack[objectIndex-1], thisObject);
                }
            }

            public override string GetName(bool isFirst)
            {
                return isFirst ? Descriptor.Name : "." + Descriptor.Name;
            }
        }

        private sealed class FieldPathItem : MemberPathItem
        {
            private readonly FieldDescriptor Descriptor;
            private readonly bool isValueType;
 
            public FieldPathItem(FieldDescriptor descriptor)
            {
                this.Descriptor = descriptor;
                isValueType = descriptor.DeclaringType.IsValueType;
            }

            public override IMemberDescriptor MemberDescriptor
            {
                get
                {
                    return Descriptor;
                }
            }

            public override object GetValue(object thisObj)
            {
                return Descriptor.Get(thisObj);
            }

            public override void SetValue(List<object> stack, int objectIndex, object thisObject, object value)
            {
                Descriptor.Set(thisObject, value);

                if (isValueType && Parent != null)
                {
                    Parent.SetValue(stack, objectIndex - 1, stack[objectIndex - 1], thisObject);
                }
            }

            public override string GetName(bool isFirst)
            {
                return "." + Descriptor.Name;
            }
        }

        private abstract class SpecialMemberPathItemBase : MemberPathItem
        {
            public override IMemberDescriptor MemberDescriptor
            {
                get
                {
                    return null;
                }
            }            
        }


        private sealed class ArrayPathItem : SpecialMemberPathItemBase
        {
            private readonly int index;

            public ArrayPathItem(int index)
            {
                this.index = index;
            }

            public override object GetValue(object thisObj)
            {
                return ((Array)thisObj).GetValue(index);
            }

            public override void SetValue(List<object> stack, int objectIndex, object thisObject, object value)
            {
                ((Array)thisObject).SetValue(value, index);
            }

            public override string GetName(bool isFirst)
            {
                return "[" + index + "]";
            }
        }

        private sealed class CollectionPathItem : SpecialMemberPathItemBase
        {
            public readonly CollectionDescriptor Descriptor;

            public readonly int Index;

            public CollectionPathItem(CollectionDescriptor descriptor, int index)
            {
                this.Descriptor = descriptor;
                this.Index = index;
            }

            public override object GetValue(object thisObj)
            {
                return Descriptor.GetValue(thisObj, Index);
            }

            public override void SetValue(List<object> stack, int objectIndex, object thisObject, object value)
            {
                Descriptor.SetValue(thisObject, Index, value);
            }

            public override string GetName(bool isFirst)
            {
                return "[" + Index + "]";
            }
        }

        private sealed class DictionaryPathItem : SpecialMemberPathItemBase
        {
            public readonly DictionaryDescriptor Descriptor;

            public readonly object Key;

            public DictionaryPathItem(DictionaryDescriptor descriptor, object key)
            {
                this.Descriptor = descriptor;
                this.Key = key;
            }

            public override object GetValue(object thisObj)
            {
                return Descriptor.GetValue(thisObj, Key);
            }

            public override void SetValue(List<object> stack, int objectIndex, object thisObject, object value)
            {
                Descriptor.SetValue(thisObject, Key, value);
            }

            public override string GetName(bool isFirst)
            {
                return "[" + Key + "]";
            }
        }
    }
}