using System;

namespace SignalR.Hubs
{
    /// <summary>
    /// Holds information about a single hub.
    /// </summary>
    public class HubDescriptor
    {
        /// <summary>
        /// Hub derivation flag.  Used to monitor changes to the hub descriptor.
        /// </summary>
        public virtual DerivedFrom Derived { get; set; }

        /// <summary>
        /// Hub name.
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// Hub type.
        /// </summary>
        public virtual Type Type { get; set; }
    }
}
