using System;
using SQLite;

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Maps a DataRow to a Amica.vNext.Objects instance.
    /// </summary>
    public class HttpMapping
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed(Name="ResourceCompanyIdLocalId", Order=1, Unique=true)]

        public string Resource { get; set; }

        [Indexed(Name="ResourceCompanyIdLocalId", Order=2, Unique=true)]
        public int? LocalCompanyId { get; set; }

        [Indexed(Name="ResourceCompanyIdLocalId", Order=3, Unique=true)]
        public int LocalId { get; set; }

        [Indexed]
        public string RemoteId { get; set; }

        public string RemoteCompanyId { get; set; }

        public string ETag { get; set; }
        [Indexed]
        public DateTime? LastUpdated { get; set; }
    }
}