/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    // TODO: remove on next Rhino.Api update
    public static class DataTableExtentions
    {
        /// <summary>
        /// Applies a data row as extra columns. The data in the data row will apply to all rows.
        /// </summary>
        /// <param name="t"><see cref="DataRow"/> to apply to.</param>
        /// <param name="dataTables">A collection of <see cref="DataTable"/> to apply.</param>
        /// <returns>New RhinoTestCase.DataSource object.</returns>
        public static IEnumerable<IDictionary<string, object>> Apply(this DataTable t, IEnumerable<DataTable> dataTables)
        {
            // exit conditions
            if (!dataTables.Any())
            {
                return t.ToDictionary();
            }

            // merge conditions
            var mergeable = new[] { t }.Concat(dataTables).GroupBy(x => x.Rows.Count).Where(g => g.Count() > 1);
            var notMergeable = new[] { t }.Concat(dataTables).GroupBy(x => x.Rows.Count).Where(g => g.Count() == 1).SelectMany(i=>i);

            var onTables = new List<DataTable>();
            foreach (var group in mergeable)
            {
                var table = group.Merge();
                onTables.Add(table);
            }

            // setup
            var onDataTable = notMergeable.Concat(onTables).ToArray();

            // setup
            var results = new List<IDictionary<string, object>>();

            // iterate
            foreach (var dataTable in onDataTable.Skip(1))
            {
                var rows = ApplyOne(onDataTable[0], dataTable);
                results.AddRange(rows);
            }

            // get
            return results.Count > 0 ? results : onDataTable[0].ToDictionary();
        }

        private static IEnumerable<IDictionary<string, object>> ApplyOne(DataTable t, DataTable dataTable)
        {
            // setup
            var target = (t.Rows.Count > dataTable.Rows.Count ? t : dataTable).ToDictionary();
            var source = (t.Rows.Count < dataTable.Rows.Count ? t : dataTable).Rows.Cast<DataRow>().ToArray();
            var results = new List<IDictionary<string, object>>();

            // apply first
            foreach (var row in target)
            {
                foreach (DataColumn column in source[0].Table.Columns)
                {
                    if (row.ContainsKey(column.ColumnName))
                    {
                        continue;
                    }
                    row[column.ColumnName] = source[0][column];
                }
                results.Add(row);
            }

            // setup
            source = source.Skip(1).ToArray();

            // apply current
            foreach (var row in source)
            {
                foreach (var item in source.SelectMany(_ => target))
                {
                    foreach (DataColumn column in row.Table.Columns)
                    {
                        if (item.ContainsKey(column.ColumnName))
                        {
                            continue;
                        }
                        item[column.ColumnName] = row[column];
                    }
                    results.Add(item);
                }
            }

            // get
            return results;
        }
    }
}