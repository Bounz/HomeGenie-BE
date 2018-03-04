using System;

namespace HomeGenie.Database
{
    public class LiteDbCollectionAttribute : Attribute
    {
        public string CollectionName { get; }

        public LiteDbCollectionAttribute(string collectionName)
        {
            CollectionName = collectionName;
        }
    }
}
