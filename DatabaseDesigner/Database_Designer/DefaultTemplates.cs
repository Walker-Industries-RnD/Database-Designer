using System;
using System.Collections.Generic;
using System.IO;

namespace Database_Designer
{
    public static class DefaultTemplates
    {
        public static readonly Dictionary<string, string> RowTemplates = new()
        {
            ["User Accounts"] = @"{
  ""Name"": ""User Accounts"",
  ""Overview"": ""A ready-to-use users table for secure sign-in: unique username and email, hashed password, and an automatic created_at timestamp."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example row template."",
  ""Data"": [
    { ""TableName"": ""users"", ""Description"": ""Application user accounts."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""username"", ""Description"": ""Unique login name"", ""RowType"": ""VarChar"", ""Limit"": 32, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""email"", ""Description"": ""Unique email address"", ""RowType"": ""VarChar"", ""Limit"": 255, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""password_hash"", ""Description"": ""Hashed password"", ""RowType"": ""Text"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""created_at"", ""Description"": ""Row creation timestamp"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""now()"", ""Check"": null, ""DefaultIsPostgresFunction"": true }
      ],
      ""References"": [], ""Indexes"": [], ""CustomRows"": [] }
  ]
}",

            ["Blog Posts"] = @"{
  ""Name"": ""Blog Posts"",
  ""Overview"": ""A simple posts table with a title, body, published flag, and timestamp."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example row template."",
  ""Data"": [
    { ""TableName"": ""posts"", ""Description"": ""Blog or article posts."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""title"", ""Description"": ""Post title"", ""RowType"": ""VarChar"", ""Limit"": 200, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""body"", ""Description"": ""Post content"", ""RowType"": ""Text"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": false, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""published"", ""Description"": ""Whether the post is visible"", ""RowType"": ""Boolean"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""false"", ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""created_at"", ""Description"": ""Row creation timestamp"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""now()"", ""Check"": null, ""DefaultIsPostgresFunction"": true }
      ],
      ""References"": [], ""Indexes"": [], ""CustomRows"": [] }
  ]
}",

            ["Inventory Items"] = @"{
  ""Name"": ""Inventory Items"",
  ""Overview"": ""A products/items table: unique SKU, name, quantity, price, and JSONB metadata."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example row template."",
  ""Data"": [
    { ""TableName"": ""items"", ""Description"": ""Products or inventory items."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""sku"", ""Description"": ""Unique stock-keeping unit"", ""RowType"": ""VarChar"", ""Limit"": 64, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""name"", ""Description"": ""Display name"", ""RowType"": ""VarChar"", ""Limit"": 200, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""quantity"", ""Description"": ""Units in stock"", ""RowType"": ""Integer"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""0"", ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""price"", ""Description"": ""Unit price"", ""RowType"": ""Numeric"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""0"", ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""metadata"", ""Description"": ""Flexible attributes"", ""RowType"": ""Jsonb"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": false, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false }
      ],
      ""References"": [], ""Indexes"": [], ""CustomRows"": [] }
  ]
}"
        };

        public static readonly Dictionary<string, string> ProjectTemplates = new()
        {
            ["Secure Sign-In"] = @"{
  ""Name"": ""Secure Sign-In"",
  ""Overview"": ""Authentication starter: a users table plus a sessions table that references it, ready for login/token flows."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example project template."",
  ""Data"": [
    { ""TableName"": ""users"", ""Description"": ""User accounts."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""username"", ""Description"": ""Unique login name"", ""RowType"": ""VarChar"", ""Limit"": 32, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""email"", ""Description"": ""Unique email"", ""RowType"": ""VarChar"", ""Limit"": 255, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""password_hash"", ""Description"": ""Hashed password"", ""RowType"": ""Text"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""created_at"", ""Description"": ""Created"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""now()"", ""Check"": null, ""DefaultIsPostgresFunction"": true }
      ], ""References"": [], ""Indexes"": [], ""CustomRows"": [] },
    { ""TableName"": ""sessions"", ""Description"": ""Active login sessions."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""user_id"", ""Description"": ""Owner"", ""RowType"": ""BigInt"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""token"", ""Description"": ""Session token"", ""RowType"": ""Text"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""expires_at"", ""Description"": ""Expiry"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false }
      ],
      ""References"": [ { ""MainTable"": ""public.sessions"", ""RefTable"": ""public.users"", ""ForeignKey"": ""user_id"", ""RefTableKey"": ""id"", ""OnDeleteAction"": ""Cascade"", ""OnUpdateAction"": ""NoAction"" } ],
      ""Indexes"": [], ""CustomRows"": [] }
  ]
}",

            ["Simple Chat"] = @"{
  ""Name"": ""Simple Chat"",
  ""Overview"": ""A minimal chat backend: users and messages, where each message references its sender."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example project template."",
  ""Data"": [
    { ""TableName"": ""users"", ""Description"": ""Chat users."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""username"", ""Description"": ""Display name"", ""RowType"": ""VarChar"", ""Limit"": 32, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false }
      ], ""References"": [], ""Indexes"": [], ""CustomRows"": [] },
    { ""TableName"": ""messages"", ""Description"": ""Chat messages."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""sender_id"", ""Description"": ""Who sent it"", ""RowType"": ""BigInt"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""body"", ""Description"": ""Message text"", ""RowType"": ""Text"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""sent_at"", ""Description"": ""Timestamp"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""now()"", ""Check"": null, ""DefaultIsPostgresFunction"": true }
      ],
      ""References"": [ { ""MainTable"": ""public.messages"", ""RefTable"": ""public.users"", ""ForeignKey"": ""sender_id"", ""RefTableKey"": ""id"", ""OnDeleteAction"": ""Cascade"", ""OnUpdateAction"": ""NoAction"" } ],
      ""Indexes"": [], ""CustomRows"": [] }
  ]
}",

            ["Simple Marketplace"] = @"{
  ""Name"": ""Simple Marketplace"",
  ""Overview"": ""A small marketplace: users, items listed by a seller, and orders placed by a buyer for an item."",
  ""AuthorName"": ""Walker Industries"", ""Company"": ""Walker Industries"", ""Website"": """", ""License"": ""MIT"", ""Note"": ""Default example project template."",
  ""Data"": [
    { ""TableName"": ""users"", ""Description"": ""Marketplace users."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""username"", ""Description"": ""Display name"", ""RowType"": ""VarChar"", ""Limit"": 32, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": true, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false }
      ], ""References"": [], ""Indexes"": [], ""CustomRows"": [] },
    { ""TableName"": ""items"", ""Description"": ""Items for sale."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""seller_id"", ""Description"": ""Seller"", ""RowType"": ""BigInt"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""name"", ""Description"": ""Item name"", ""RowType"": ""VarChar"", ""Limit"": 200, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""price"", ""Description"": ""Price"", ""RowType"": ""Numeric"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""0"", ""Check"": null, ""DefaultIsPostgresFunction"": false }
      ],
      ""References"": [ { ""MainTable"": ""public.items"", ""RefTable"": ""public.users"", ""ForeignKey"": ""seller_id"", ""RefTableKey"": ""id"", ""OnDeleteAction"": ""Cascade"", ""OnUpdateAction"": ""NoAction"" } ],
      ""Indexes"": [], ""CustomRows"": [] },
    { ""TableName"": ""orders"", ""Description"": ""Purchases."", ""SchemaName"": ""public"",
      ""Rows"": [
        { ""Name"": ""id"", ""Description"": ""Primary key"", ""RowType"": ""BigSerial"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": true, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""buyer_id"", ""Description"": ""Buyer"", ""RowType"": ""BigInt"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""item_id"", ""Description"": ""Item ordered"", ""RowType"": ""BigInt"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": null, ""Check"": null, ""DefaultIsPostgresFunction"": false },
        { ""Name"": ""ordered_at"", ""Description"": ""Timestamp"", ""RowType"": ""TimestampTz"", ""Limit"": null, ""IsArray"": false, ""ArrayLimit"": null, ""EncryptedAndNOTMedia"": false, ""Media"": false, ""IsPrimary"": false, ""IsUnique"": false, ""IsNotNull"": true, ""DefaultValue"": ""now()"", ""Check"": null, ""DefaultIsPostgresFunction"": true }
      ],
      ""References"": [
        { ""MainTable"": ""public.orders"", ""RefTable"": ""public.users"", ""ForeignKey"": ""buyer_id"", ""RefTableKey"": ""id"", ""OnDeleteAction"": ""Cascade"", ""OnUpdateAction"": ""NoAction"" },
        { ""MainTable"": ""public.orders"", ""RefTable"": ""public.items"", ""ForeignKey"": ""item_id"", ""RefTableKey"": ""id"", ""OnDeleteAction"": ""Cascade"", ""OnUpdateAction"": ""NoAction"" }
      ],
      ""Indexes"": [], ""CustomRows"": [] }
  ]
}"
        };

        public static void Install(string userFolder)
        {
            try
            {
                WritePacks(Path.Combine(userFolder, "Row Templates"), RowTemplates);
                WritePacks(Path.Combine(userFolder, "Project Templates"), ProjectTemplates);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultTemplates] Install failed: {ex.Message}");
            }
        }

        private static void WritePacks(string root, Dictionary<string, string> packs)
        {
            Directory.CreateDirectory(root);
            foreach (var pack in packs)
            {
                var packFolder = Path.Combine(root, pack.Key);
                if (Directory.Exists(packFolder)) continue;
                var versionFolder = Path.Combine(packFolder, "v1");
                Directory.CreateDirectory(versionFolder);
                File.WriteAllText(Path.Combine(versionFolder, "Template.DsgnRowTmplate"), pack.Value);
            }
        }
    }
}
