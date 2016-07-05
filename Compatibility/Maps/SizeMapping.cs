using System;
using System.Collections.Generic;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class SizeMapping : Mapping
    {

        internal SizeMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
            Fields.Add("Taglia1", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 1),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 1)});
            Fields.Add("Taglia2", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 2),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 2)});
            Fields.Add("Taglia3", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 3),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 3)});
            Fields.Add("Taglia4", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 4),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 4)});
            Fields.Add("Taglia5", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 5),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 5)});
            Fields.Add("Taglia6", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 6),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 6)});
            Fields.Add("Taglia7", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 7),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 7)});
            Fields.Add("Taglia8", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 8),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 8)});
            Fields.Add("Taglia9", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 9),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 9)});
            Fields.Add("Taglia10", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 10),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 10)});
            Fields.Add("Taglia11", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 11),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 11)});
            Fields.Add("Taglia12", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 12),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 12)});
            Fields.Add("Taglia13", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 13),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 13)});
            Fields.Add("Taglia14", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 14),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 14)});
            Fields.Add("Taglia15", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 15),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 15)});
            Fields.Add("Taglia16", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 16),
				UpstreamTransform = (x, obj) => SetNumberCollectionItem(x, obj, 16)});
        }
		private object SetTaglia(object obj, int i)
        {
            var l = (List<string>)obj;
            if (i > l.Count) return null;
            return l[i-1];

        }
		private object SetNumberCollectionItem(object value, object obj, int i)
        {
            var size = (Size)obj;
			if (size.NumberCollection.Count > i)
            {
                if (value == DBNull.Value)
                    size.NumberCollection.RemoveAt(i-1);
				else
					size.NumberCollection[i - 1] = (string)value;
            }
            else
            {
				if (value != DBNull.Value)
					size.NumberCollection.Add((string)value);
            }
            return size.NumberCollection;
        }
    }

}
