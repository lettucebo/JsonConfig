using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using Newtonsoft.Json;

namespace JsonConfig.Core
{
    public class JsonConfigObject: DynamicObject, IDictionary<string, object>
    {
        internal Dictionary<string, object> members = new Dictionary<string, object>();
        public static JsonConfigObject FromExpandObject(ExpandoObject e)
        {
            var edict = e as IDictionary<string, object>;
            var c = new JsonConfigObject();
            var cdict = (IDictionary<string, object>)c;

            // this is not complete. It will, however work for JsonFX ExpandoObjects
            // which consits only of primitive types, ExpandoObject or ExpandoObject [] 
            // but won't work for generic ExpandoObjects which might include collections etc.
            foreach (var kvp in edict)
            {
                // recursively convert and add ExpandoObjects
                if (kvp.Value is ExpandoObject)
                {
                    cdict.Add(kvp.Key, FromExpandObject((ExpandoObject)kvp.Value));
                }
                else if (kvp.Value is ExpandoObject[])
                {
                    var config_objects = new List<JsonConfigObject>();
                    foreach (var ex in ((ExpandoObject[])kvp.Value))
                    {
                        config_objects.Add(FromExpandObject(ex));
                    }
                    cdict.Add(kvp.Key, config_objects.ToArray());
                }
                else
                    cdict.Add(kvp.Key, kvp.Value);
            }
            return c;
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (this.members.ContainsKey(binder.Name))
                result = this.members[binder.Name];
            else
                result = null;

            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (this.members.ContainsKey(binder.Name))
                this.members[binder.Name] = value;
            else
                this.members.Add(binder.Name, value);
            return true;
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            // some special methods that should be in our dynamic object
            if (binder.Name == "ApplyJsonFromFile" && args.Length == 1 && args[0] is string)
            {
                result = JsonConfig.ApplyJsonFromFileInfo(new FileInfo((string)args[0]), this);
                return true;
            }
            if (binder.Name == "ApplyJsonFromFile" && args.Length == 1 && args[0] is FileInfo)
            {
                result = JsonConfig.ApplyJsonFromFileInfo((FileInfo)args[0], this);
                return true;
            }
            if (binder.Name == "Clone")
            {
                result = this.Clone();
                return true;
            }
            if (binder.Name == "Exists" && args.Length == 1 && args[0] is string)
            {
                result = this.members.ContainsKey((string)args[0]);
                return true;
            }

            // no other methods availabe, error
            result = null;
            return false;

        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this.members);
        }
        public void ApplyJson(string json)
        {
            JsonConfigObject result = JsonConfig.ApplyJson(json, this);
            // replace myself's members with the new ones
            this.members = result.members;
        }
        public static implicit operator JsonConfigObject(ExpandoObject exp)
        {
            return JsonConfigObject.FromExpandObject(exp);
        }
        #region IEnumerable implementation
        public System.Collections.IEnumerator GetEnumerator()
        {
            return this.members.GetEnumerator();
        }
        #endregion

        #region IEnumerable implementation
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return this.members.GetEnumerator();
        }
        #endregion

        #region ICollection implementation
        public void Add(KeyValuePair<string, object> item)
        {
            this.members.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            this.members.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return this.members.ContainsKey(item.Key) && this.members[item.Key] == item.Value;
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            throw new System.NotImplementedException();
        }

        public int Count
        {
            get
            {
                return this.members.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }
        #endregion

        #region IDictionary implementation
        public void Add(string key, object value)
        {
            this.members.Add(key, value);
        }

        public bool ContainsKey(string key)
        {
            return this.members.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return this.members.Remove(key);
        }

        public object this[string key]
        {
            get
            {
                return this.members[key];
            }
            set
            {
                this.members[key] = value;
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                return this.members.Keys;
            }
        }

        public ICollection<object> Values
        {
            get
            {
                return this.members.Values;
            }
        }
        public bool TryGetValue(string key, out object value)
        {
            return this.members.TryGetValue(key, out value);
        }

        #region ICloneable implementation

        object Clone()
        {
            return Merger.Merge(new JsonConfigObject(), this);
        }

        #endregion
        #endregion

        #region casts
        public static implicit operator bool(JsonConfigObject c)
        {
            // we want to test for a member:
            // if (config.SomeMember) { ... }
            //
            // instead of:
            // if (config.SomeMember != null) { ... }

            // we return always true, because a NullExceptionPreventer is returned when member
            // does not exist
            return true;
        }
        #endregion
    }
}