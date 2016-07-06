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
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 1)});
            Fields.Add("Taglia2", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 2),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 2)});
            Fields.Add("Taglia3", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 3),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 3)});
            Fields.Add("Taglia4", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 4),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 4)});
            Fields.Add("Taglia5", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 5),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 5)});
            Fields.Add("Taglia6", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 6),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 6)});
            Fields.Add("Taglia7", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 7),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 7)});
            Fields.Add("Taglia8", new FieldMapping
            {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 8),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 8)});
            Fields.Add("Taglia9", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 9),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 9)});
            Fields.Add("Taglia10", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 10),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 10)});
            Fields.Add("Taglia11", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 11),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 11)});
            Fields.Add("Taglia12", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 12),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 12)});
            Fields.Add("Taglia13", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 13),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 13)});
            Fields.Add("Taglia14", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 14),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 14)});
            Fields.Add("Taglia15", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 15),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 15)});
            Fields.Add("Taglia16", new FieldMapping {
                PropertyName = "NumberCollection",
                DownstreamTransform = (x) => SetTaglia(x, 16),
				UpstreamTransform = (key, row, obj) => SetNumberCollectionItem(row[key], obj, 16)});
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
