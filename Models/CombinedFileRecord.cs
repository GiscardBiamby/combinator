﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Orchard.Environment.Extensions;
using Piedone.Combinator.Helpers;

namespace Piedone.Combinator.Models
{
    [OrchardFeature("Piedone.Combinator")]
    public class CombinedFileRecord
    {
        public virtual int Id { get; set; }
        public virtual int HashCode { get; set; }
        public virtual int Slice { get; set; }
        public virtual ResourceType Type { get; set; }
        public virtual DateTime? LastUpdatedUtc { get; set; }
    }
}