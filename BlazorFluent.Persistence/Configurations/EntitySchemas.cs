using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorFluent.Persistence.Configurations
{
    /// <summary>
    /// Schema to be used by each table. Follow lower case format
    /// </summary>
    internal enum EntitySchemas
    {
        identity, ///all tables related to a user identity and login details
        tenancy, //Tenanacy level tables only
        app,//App level tables
    }
}
