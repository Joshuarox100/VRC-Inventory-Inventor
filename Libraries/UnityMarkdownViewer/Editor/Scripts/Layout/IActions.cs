////////////////////////////////////////////////////////////////////////////////

using UnityEngine;

namespace InventoryInventor.Libraries.MG.MDV
{
    public interface IActions
    {
        Texture FetchImage( string url );
        void    SelectPage( string url );
    }
}

