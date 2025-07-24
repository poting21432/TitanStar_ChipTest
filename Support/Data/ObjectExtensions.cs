using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Support;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
namespace Support
{
    public static class ObjectExtensions
    {
        public static void SerializeToJson<T>(this T obj, string filePath)
        {
            string jsonString = JsonSerializer.Serialize(obj);
            File.WriteAllText(filePath, jsonString);
        }

        public static T? DeserializeFromJson<T>(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return default;
            string jsonString = File.ReadAllText(filePath);
            T? data = JsonSerializer.Deserialize<T>(jsonString);
            return data;
        }
        public static bool UntilTrue(this Func<bool>? FuncWork,int max_retry = 0)
        {
            int retry = 0;
            while(FuncWork?.Invoke() ?? false)
            {
                if(max_retry > 0 && retry>=max_retry)
                    return false;
                retry++;
            }
            return true;
        }


        private static readonly MethodInfo? CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void CopyTo<T>(this T? obj, T? target) where T : new()
        {
            if (obj == null || target == null)
                return;
            Type type = obj.GetType();
            PropertyInfo[]? properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                // 檢查屬性是否可讀寫
                if (property.CanRead && property.CanWrite)
                {
                    // 讀取源物件的屬性值並設定到目標物件的對應屬性
                    object? value = property.GetValue(obj);
                    property.SetValue(target, value);
                }
            }
        }
        public static object? GetProperty<T>(this T? obj, string Property) where T : new()
        {
            Type myType = typeof(T);
            PropertyInfo? myPropInfo = myType.GetProperty(Property);
            return myPropInfo?.GetValue(obj, null);
        }
        public static void SetProperty<T>(this T? obj, string Property, object value) where T : new()
        {
            Type myType = typeof(T);
            PropertyInfo? myPropInfo = myType.GetProperty(Property);
            myPropInfo?.SetValue(obj, value);
        }
        public static bool IsPrimitive(this Type type)
        {
            if (type == typeof(String)) return true;
            return (type.IsValueType & type.IsPrimitive);
        }

        public static object? Copy(this object? originalObject)
        {
            if (originalObject != null)
                return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
            else
                return null;
        }
        private static Object? InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
        {
            if (originalObject == null)
                return null;
            var typeToReflect = originalObject.GetType();
            if (IsPrimitive(typeToReflect))
                return originalObject;
            if (visited.ContainsKey(originalObject))
                return visited[originalObject];
            if (typeof(Delegate).IsAssignableFrom(typeToReflect))
                return null;
            var cloneObject = CloneMethod?.Invoke(originalObject, null);
            if (cloneObject == null)
                return null;
            if (typeToReflect.IsArray)
            {
                Type? arrayType = typeToReflect.GetElementType();
                if (arrayType != null)
                    if (IsPrimitive(arrayType) == false)
                    {
                        Array clonedArray = (Array)cloneObject;
                        clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                    }
            }
            visited.Add(originalObject, cloneObject);
            CopyFields(originalObject, visited, cloneObject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
            return cloneObject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
        {
            if (typeToReflect.BaseType != null)
            {
                RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
                CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
            }
        }

        private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
        {
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
            {
                if (filter != null && filter(fieldInfo) == false) continue;
                if (IsPrimitive(fieldInfo.FieldType)) continue;
                var originalFieldValue = fieldInfo.GetValue(originalObject);
                var clonedFieldValue = InternalCopy(originalFieldValue, visited);
                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }
        }
        public static T? Copy<T>(this T original)
        {
            return (T?)Copy((object?)original);
        }

        public static bool StrEquals(this object? orig, object? other)
            => (orig == null || other == null) ? false : orig.ToString() == other.ToString();
    }

    public class ReferenceEqualityComparer : EqualityComparer<Object>
    {
        public override bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }
        public override int GetHashCode(object obj)
        {
            if (obj == null) return 0;
            return obj.GetHashCode();
        }
    }

    
}