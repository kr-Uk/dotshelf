using System.Collections.Generic;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public interface IPostItStyleCatalog
    {
        IReadOnlyList<PostItStyle> GetAllStyles();

        PostItStyle GetStyle(long assetId);

        bool TryGetStyle(long assetId, out PostItStyle style);
    }
}
