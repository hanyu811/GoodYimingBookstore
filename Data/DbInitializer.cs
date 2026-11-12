using BookStoreApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Data;

public static class DbInitializer
{
    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();

        db.Database.EnsureCreated();

        db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[Books]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Books', 'CoverUrl') IS NULL
        ALTER TABLE [dbo].[Books] ADD [CoverUrl] NVARCHAR(200) NOT NULL CONSTRAINT DF_Books_CoverUrl DEFAULT(N'');

    IF COL_LENGTH('dbo.Books', 'Category') IS NULL
        ALTER TABLE [dbo].[Books] ADD [Category] NVARCHAR(50) NOT NULL CONSTRAINT DF_Books_Category DEFAULT(N'未分类');

    IF COL_LENGTH('dbo.Books', 'Stock') IS NULL
        ALTER TABLE [dbo].[Books] ADD [Stock] INT NOT NULL CONSTRAINT DF_Books_Stock DEFAULT(0);

    IF COL_LENGTH('dbo.Books', 'ImageUrl') IS NULL
        ALTER TABLE [dbo].[Books] ADD [ImageUrl] NVARCHAR(200) NULL;

    IF COL_LENGTH('dbo.Books', 'IsListed') IS NULL
        ALTER TABLE [dbo].[Books] ADD [IsListed] BIT NOT NULL CONSTRAINT DF_Books_IsListed DEFAULT(1);

    IF COL_LENGTH('dbo.Books', 'ImageUrl') IS NOT NULL AND COL_LENGTH('dbo.Books', 'CoverUrl') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE [dbo].[Books]
              SET [ImageUrl] = CASE WHEN [ImageUrl] IS NULL OR [ImageUrl] = '''' THEN [CoverUrl] ELSE [ImageUrl] END,
                  [CoverUrl] = CASE WHEN [CoverUrl] IS NULL OR [CoverUrl] = '''' THEN ISNULL([ImageUrl], '''') ELSE [CoverUrl] END;');
    END

    IF COL_LENGTH('dbo.Books', 'CreatedAt') IS NULL
        ALTER TABLE [dbo].[Books] ADD [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_Books_CreatedAt DEFAULT(GETDATE());
END

IF OBJECT_ID(N'[dbo].[Books]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Books] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Title] NVARCHAR(200) NOT NULL,
        [Author] NVARCHAR(100) NULL,
        [Category] NVARCHAR(50) NOT NULL CONSTRAINT DF_Books_Category DEFAULT(N'未分类'),
        [Price] DECIMAL(10,2) NOT NULL,
        [Stock] INT NOT NULL CONSTRAINT DF_Books_Stock DEFAULT(0),
        [Description] NVARCHAR(500) NULL,
        [CoverUrl] NVARCHAR(200) NOT NULL CONSTRAINT DF_Books_CoverUrl DEFAULT(N''),
        [ImageUrl] NVARCHAR(200) NULL,
        [IsListed] BIT NOT NULL CONSTRAINT DF_Books_IsListed DEFAULT(1),
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_Books_CreatedAt DEFAULT(GETDATE())
    );
END

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserName] NVARCHAR(100) NOT NULL,
        [PasswordHash] NVARCHAR(200) NOT NULL,
        [Role] NVARCHAR(20) NOT NULL CONSTRAINT DF_Users_Role DEFAULT(N'reader'),
        [DisplayName] NVARCHAR(100) NOT NULL CONSTRAINT DF_Users_DisplayName DEFAULT(N''),
        [Address] NVARCHAR(200) NULL,
        [PhoneNumber] NVARCHAR(20) NULL
    );
END

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Users', 'UserName') IS NULL
        ALTER TABLE [dbo].[Users] ADD [UserName] NVARCHAR(100) NOT NULL CONSTRAINT DF_Users_UserName DEFAULT(N'user');

    IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NULL
        ALTER TABLE [dbo].[Users] ADD [PasswordHash] NVARCHAR(200) NOT NULL CONSTRAINT DF_Users_PasswordHash DEFAULT(N'demo');

    IF COL_LENGTH('dbo.Users', 'Role') IS NULL
        ALTER TABLE [dbo].[Users] ADD [Role] NVARCHAR(20) NOT NULL CONSTRAINT DF_Users_Role DEFAULT(N'reader');

    IF COL_LENGTH('dbo.Users', 'DisplayName') IS NULL
        ALTER TABLE [dbo].[Users] ADD [DisplayName] NVARCHAR(100) NOT NULL CONSTRAINT DF_Users_DisplayName DEFAULT(N'');

    IF COL_LENGTH('dbo.Users', 'Address') IS NULL
        ALTER TABLE [dbo].[Users] ADD [Address] NVARCHAR(200) NULL;

    IF COL_LENGTH('dbo.Users', 'PhoneNumber') IS NULL
        ALTER TABLE [dbo].[Users] ADD [PhoneNumber] NVARCHAR(20) NULL;

        IF COL_LENGTH('dbo.Users', 'DisplayName') IS NOT NULL
        BEGIN
          EXEC(N'UPDATE [dbo].[Users]
              SET [DisplayName] = [UserName]
              WHERE [DisplayName] IS NULL OR LTRIM(RTRIM([DisplayName])) = '''';');
        END

        IF COL_LENGTH('dbo.Users', 'Role') IS NOT NULL
        BEGIN
          EXEC(N'UPDATE [dbo].[Users]
              SET [Role] = N''reader''
              WHERE [Role] IS NULL OR LTRIM(RTRIM([Role])) = '''';');

          EXEC(N'UPDATE [dbo].[Users]
              SET [Role] = N''reader''
              WHERE LOWER([Role]) = ''user'';');

          EXEC(N'UPDATE [dbo].[Users]
              SET [Role] = N''seller''
              WHERE LOWER([Role]) IN (''merchant'', ''shop'');');
        END

    IF COL_LENGTH('dbo.Users', 'Password') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE [dbo].[Users]
              SET [PasswordHash] = [Password]
              WHERE ([PasswordHash] IS NULL OR LTRIM(RTRIM([PasswordHash])) = '''')
                AND [Password] IS NOT NULL
                AND LTRIM(RTRIM([Password])) <> '''';');

        DECLARE @UsersPasswordDefaultName NVARCHAR(128);
        SELECT @UsersPasswordDefaultName = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Users]') AND c.name = N'Password';

        IF @UsersPasswordDefaultName IS NULL
            EXEC(N'ALTER TABLE [dbo].[Users] ADD CONSTRAINT DF_Users_Password DEFAULT(N''demo'') FOR [Password]');
    END

    DECLARE @UsersRoleDefaultName NVARCHAR(128);
    SELECT @UsersRoleDefaultName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Users]') AND c.name = N'Role';

    IF @UsersRoleDefaultName IS NULL
        EXEC(N'ALTER TABLE [dbo].[Users] ADD CONSTRAINT DF_Users_Role DEFAULT(N''reader'') FOR [Role]');
END

IF OBJECT_ID(N'[dbo].[Carts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Carts] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [ReaderUserName] NVARCHAR(100) NOT NULL,
        [BookId] INT NOT NULL,
        [Quantity] INT NOT NULL,
        CONSTRAINT FK_Carts_Users FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]),
        CONSTRAINT FK_Carts_Books FOREIGN KEY ([BookId]) REFERENCES [dbo].[Books]([Id])
    );
END

IF OBJECT_ID(N'[dbo].[Carts]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Carts', 'ReaderUserName') IS NULL
        ALTER TABLE [dbo].[Carts] ADD [ReaderUserName] NVARCHAR(100) NULL;

    IF COL_LENGTH('dbo.Carts', 'ReaderUserName') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE c
              SET c.[ReaderUserName] = u.[UserName]
              FROM [dbo].[Carts] c
              INNER JOIN [dbo].[Users] u ON c.[UserId] = u.[Id]
              WHERE c.[ReaderUserName] IS NULL OR LTRIM(RTRIM(c.[ReaderUserName])) = '''';');
    END

    IF COL_LENGTH('dbo.Carts', 'ReaderCardId') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE c
              SET c.[ReaderUserName] = u.[UserName]
              FROM [dbo].[Carts] c
              INNER JOIN [dbo].[Users] u ON c.[ReaderCardId] = u.[Id]
              WHERE c.[ReaderUserName] IS NULL OR LTRIM(RTRIM(c.[ReaderUserName])) = '''';');
    END

    EXEC(N'UPDATE [dbo].[Carts]
          SET [ReaderUserName] = N''reader-'' + CAST([UserId] AS NVARCHAR(20))
          WHERE [ReaderUserName] IS NULL OR LTRIM(RTRIM([ReaderUserName])) = '''';');

    IF COL_LENGTH('dbo.Carts', 'ReaderUserName') IS NOT NULL
        ALTER TABLE [dbo].[Carts] ALTER COLUMN [ReaderUserName] NVARCHAR(100) NOT NULL;

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Carts_Readers'
          AND parent_object_id = OBJECT_ID(N'[dbo].[Carts]'))
    BEGIN
        ALTER TABLE [dbo].[Carts] DROP CONSTRAINT [FK_Carts_Readers];
    END

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Carts_ReaderCardId_BookId'
          AND object_id = OBJECT_ID(N'[dbo].[Carts]'))
    BEGIN
        DROP INDEX [IX_Carts_ReaderCardId_BookId] ON [dbo].[Carts];
    END

    IF COL_LENGTH('dbo.Carts', 'ReaderCardId') IS NOT NULL
        ALTER TABLE [dbo].[Carts] DROP COLUMN [ReaderCardId];

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Carts_ReaderUserName_BookId'
          AND object_id = OBJECT_ID(N'[dbo].[Carts]'))
    BEGIN
        CREATE INDEX [IX_Carts_ReaderUserName_BookId] ON [dbo].[Carts]([ReaderUserName], [BookId]);
    END
END

IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [OrderDate] DATETIME NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT(GETDATE()),
        [TotalAmount] DECIMAL(10,2) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT(N'Unshipped'),
        [ShippingAddress] NVARCHAR(200) NOT NULL CONSTRAINT DF_Orders_ShippingAddress DEFAULT(N''),
        [ShippingPhone] NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_ShippingPhone DEFAULT(N''),
        [ShippedAt] DATETIME NULL,
        [ReceivedAt] DATETIME NULL,
        CONSTRAINT FK_Orders_Users FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
    );
END

IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Orders', 'ShippingAddress') IS NULL
        ALTER TABLE [dbo].[Orders] ADD [ShippingAddress] NVARCHAR(200) NOT NULL CONSTRAINT DF_Orders_ShippingAddress DEFAULT(N'');

    IF COL_LENGTH('dbo.Orders', 'ShippingPhone') IS NULL
        ALTER TABLE [dbo].[Orders] ADD [ShippingPhone] NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_ShippingPhone DEFAULT(N'');

    IF COL_LENGTH('dbo.Orders', 'ShippedAt') IS NULL
        ALTER TABLE [dbo].[Orders] ADD [ShippedAt] DATETIME NULL;

    IF COL_LENGTH('dbo.Orders', 'ReceivedAt') IS NULL
        ALTER TABLE [dbo].[Orders] ADD [ReceivedAt] DATETIME NULL;

    IF COL_LENGTH('dbo.Orders', 'Status') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE [dbo].[Orders]
              SET [Status] = N''Unshipped''
              WHERE [Status] IS NULL OR LTRIM(RTRIM([Status])) = '''';');

        EXEC(N'UPDATE [dbo].[Orders]
              SET [Status] = N''Unshipped''
              WHERE LOWER([Status]) IN (''pending'', ''paid'', N''未发货'');');

        EXEC(N'UPDATE [dbo].[Orders]
              SET [Status] = N''Shipped''
              WHERE LOWER([Status]) IN (''shipped'', N''已发货'');');

        EXEC(N'UPDATE [dbo].[Orders]
              SET [Status] = N''Received''
              WHERE LOWER([Status]) IN (''received'', ''delivered'', N''已收货'');');

        DECLARE @OrdersStatusDefaultName NVARCHAR(128);
        SELECT @OrdersStatusDefaultName = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Orders]') AND c.name = N'Status';

        IF @OrdersStatusDefaultName IS NOT NULL
            EXEC(N'ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [' + @OrdersStatusDefaultName + ']');

        EXEC(N'ALTER TABLE [dbo].[Orders] ADD CONSTRAINT DF_Orders_Status DEFAULT(N''Unshipped'') FOR [Status]');
    END
END

IF OBJECT_ID(N'[dbo].[OrderItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderItems] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [OrderId] INT NOT NULL,
        [BookId] INT NOT NULL,
        [Quantity] INT NOT NULL,
        [Price] DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_OrderItems_Orders FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id]),
        CONSTRAINT FK_OrderItems_Books FOREIGN KEY ([BookId]) REFERENCES [dbo].[Books]([Id])
    );
END
");

        EnsureSeedData(db);
    }

    public static void EnsureSeedData(BookStoreDbContext db)
    {
        EnsureUsers(db);
        EnsureBooks(db);
        EnsureDefaultOrder(db);
    }

    private static void EnsureUsers(BookStoreDbContext db)
    {
        var existingUsers = db.Users
            .ToList()
            .ToDictionary(u => NormalizeKey(u.UserName), u => u, StringComparer.Ordinal);

        var shouldSave = false;
        var newUsers = new List<User>();

        foreach (var seed in SeedUsers)
        {
            var userNameKey = NormalizeKey(seed.UserName);
            if (existingUsers.TryGetValue(userNameKey, out var existing))
            {
                if (!string.Equals(existing.Role, seed.Role, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Role = seed.Role;
                    shouldSave = true;
                }

                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                {
                    existing.DisplayName = seed.DisplayName;
                    shouldSave = true;
                }

                if (existing.PasswordHash == "demo" || string.IsNullOrWhiteSpace(existing.PasswordHash))
                {
                    existing.PasswordHash = seed.Password;
                    shouldSave = true;
                }

                continue;
            }

            newUsers.Add(new User
            {
                UserName = seed.UserName,
                PasswordHash = seed.Password,
                Role = seed.Role,
                DisplayName = seed.DisplayName
            });

            shouldSave = true;
            existingUsers[userNameKey] = newUsers[^1];
        }

        if (newUsers.Count > 0)
        {
            db.Users.AddRange(newUsers);
        }

        if (shouldSave)
        {
            db.SaveChanges();
        }
    }

    private static void EnsureBooks(BookStoreDbContext db)
    {
        if (db.Books.Any())
        {
            return;
        }

        var newBooks = new List<Book>();

        foreach (var seed in SeedBooks)
        {
            var imageUrl = $"https://picsum.photos/seed/{seed.ImageSeed}/320/420";
            newBooks.Add(new Book
            {
                Title = seed.Title,
                Author = seed.Author,
                Category = seed.Category,
                Price = seed.Price,
                Stock = seed.Stock,
                Description = seed.Description,
                CoverUrl = imageUrl,
                ImageUrl = imageUrl,
                IsListed = true
            });
        }

        if (newBooks.Count > 0)
        {
            db.Books.AddRange(newBooks);
            db.SaveChanges();
        }
    }

    private static void EnsureDefaultOrder(BookStoreDbContext db)
    {
        if (db.Orders.Any() || !db.Users.Any() || !db.Books.Any())
        {
            return;
        }

        var userId = db.Users.OrderBy(u => u.Id).Select(u => u.Id).First();
        var firstBook = db.Books.OrderBy(b => b.Id).First();

        var order = new Order
        {
            UserId = userId,
            TotalAmount = firstBook.Price,
            Status = OrderStatus.Unshipped,
            ShippingAddress = "待完善地址",
            ShippingPhone = "0000000000"
        };

        db.Orders.Add(order);
        db.SaveChanges();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            BookId = firstBook.Id,
            Quantity = 1,
            Price = firstBook.Price
        });
        db.SaveChanges();
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed record SeedUser(string UserName, string Password, string Role, string DisplayName);

    private sealed record SeedBook(
        string Title,
        string Author,
        string Category,
        decimal Price,
        int Stock,
        string Description,
        string ImageSeed);

    private static readonly SeedUser[] SeedUsers =
    [
        new SeedUser("reader1", "123456", "reader", "示例读者"),
        new SeedUser("reader2", "123456", "reader", "书友小林"),
        new SeedUser("reader3", "123456", "reader", "书友阿宁"),
        new SeedUser("reader4", "123456", "reader", "书友晨曦"),

        new SeedUser("seller1", "123456", "seller", "示例商家"),
        new SeedUser("seller2", "123456", "seller", "星河图书商家"),
        new SeedUser("seller3", "123456", "seller", "墨香图书商家"),
        new SeedUser("seller4", "123456", "seller", "远山图书商家"),

        new SeedUser("admin", "123456", "admin", "系统管理员"),
        new SeedUser("admin2", "123456", "admin", "内容审核管理员"),
        new SeedUser("admin3", "123456", "admin", "运营管理员"),
        new SeedUser("admin4", "123456", "admin", "系统安全管理员")
    ];

    private static readonly SeedBook[] SeedBooks =
    [
        new SeedBook("红楼梦", "曹雪芹", "中国名著", 98.00m, 180, "中国古典文学巅峰之作。", "hym-b001"),
        new SeedBook("西游记", "吴承恩", "中国名著", 88.00m, 170, "神魔叙事与成长冒险经典。", "hym-b002"),
        new SeedBook("三国演义", "罗贯中", "中国名著", 89.00m, 168, "群雄并起的历史战争史诗。", "hym-b003"),
        new SeedBook("水浒传", "施耐庵", "中国名著", 86.00m, 165, "梁山群像与江湖义气传奇。", "hym-b004"),
        new SeedBook("朝花夕拾", "鲁迅", "中国文学", 36.00m, 150, "鲁迅回忆性散文代表作。", "hym-b005"),
        new SeedBook("呐喊", "鲁迅", "中国文学", 39.00m, 148, "中国现代小说开创性作品集。", "hym-b006"),
        new SeedBook("边城", "沈从文", "中国文学", 35.00m, 146, "湘西风土与人性之美。", "hym-b007"),
        new SeedBook("骆驼祥子", "老舍", "中国文学", 42.00m, 144, "小人物命运的现实主义经典。", "hym-b008"),
        new SeedBook("围城", "钱钟书", "中国文学", 46.00m, 142, "现代都市知识分子众生相。", "hym-b009"),
        new SeedBook("平凡的世界", "路遥", "中国文学", 99.00m, 140, "普通人的奋斗与时代变迁。", "hym-b010"),
        new SeedBook("活着", "余华", "中国文学", 48.00m, 138, "生命韧性与苦难叙事经典。", "hym-b011"),
        new SeedBook("百年孤独", "加西亚·马尔克斯", "外国名著", 72.00m, 136, "魔幻现实主义代表作。", "hym-b012"),
        new SeedBook("1984", "乔治·奥威尔", "外国名著", 52.00m, 134, "反乌托邦文学里程碑。", "hym-b013"),
        new SeedBook("动物农场", "乔治·奥威尔", "外国名著", 38.00m, 132, "政治寓言经典。", "hym-b014"),
        new SeedBook("小王子", "安托万·德·圣埃克苏佩里", "外国名著", 36.00m, 130, "跨年龄读者喜爱的童话经典。", "hym-b015"),
        new SeedBook("老人与海", "欧内斯特·海明威", "外国名著", 42.00m, 128, "硬汉精神与人性光辉。", "hym-b016"),
        new SeedBook("悲惨世界", "维克多·雨果", "外国名著", 118.00m, 126, "人道主义长篇巨著。", "hym-b017"),
        new SeedBook("巴黎圣母院", "维克多·雨果", "外国名著", 79.00m, 124, "浪漫主义文学代表作。", "hym-b018"),
        new SeedBook("战争与和平", "列夫·托尔斯泰", "外国名著", 128.00m, 122, "俄国文学史诗级巨作。", "hym-b019"),
        new SeedBook("安娜卡列尼娜", "列夫·托尔斯泰", "外国名著", 98.00m, 120, "现实主义文学经典。", "hym-b020"),
        new SeedBook("复活", "列夫·托尔斯泰", "外国名著", 68.00m, 118, "灵魂救赎主题名作。", "hym-b021"),
        new SeedBook("罪与罚", "费奥多尔·陀思妥耶夫斯基", "外国名著", 69.00m, 116, "心理刻画深刻的经典小说。", "hym-b022"),
        new SeedBook("卡拉马佐夫兄弟", "费奥多尔·陀思妥耶夫斯基", "外国名著", 108.00m, 114, "哲思与人性冲突巨著。", "hym-b023"),
        new SeedBook("变形记", "弗朗茨·卡夫卡", "外国名著", 34.00m, 112, "现代主义文学代表作。", "hym-b024"),
        new SeedBook("局外人", "阿尔贝·加缪", "外国名著", 39.00m, 110, "存在主义文学经典。", "hym-b025"),
        new SeedBook("月亮与六便士", "威廉·萨默塞特·毛姆", "外国名著", 46.00m, 108, "理想与现实冲突的名作。", "hym-b026"),
        new SeedBook("傲慢与偏见", "简·奥斯汀", "外国名著", 45.00m, 106, "爱情与社会观察经典。", "hym-b027"),
        new SeedBook("简爱", "夏洛蒂·勃朗特", "外国名著", 46.00m, 104, "女性成长与独立意识经典。", "hym-b028"),
        new SeedBook("呼啸山庄", "艾米莉·勃朗特", "外国名著", 43.00m, 102, "充满激情与宿命感的小说。", "hym-b029"),
        new SeedBook("雾都孤儿", "查尔斯·狄更斯", "外国名著", 49.00m, 100, "维多利亚时代社会写实名著。", "hym-b030"),
        new SeedBook("双城记", "查尔斯·狄更斯", "外国名著", 56.00m, 98, "法国大革命背景下的人性故事。", "hym-b031"),
        new SeedBook("大卫科波菲尔", "查尔斯·狄更斯", "外国名著", 66.00m, 96, "狄更斯自传色彩代表作。", "hym-b032"),
        new SeedBook("鲁滨逊漂流记", "丹尼尔·笛福", "外国名著", 40.00m, 94, "冒险文学开创性作品。", "hym-b033"),
        new SeedBook("格列佛游记", "乔纳森·斯威夫特", "外国名著", 39.00m, 92, "讽刺文学经典。", "hym-b034"),
        new SeedBook("汤姆索亚历险记", "马克·吐温", "外国名著", 38.00m, 90, "少年冒险故事经典。", "hym-b035"),
        new SeedBook("哈克贝利费恩历险记", "马克·吐温", "外国名著", 42.00m, 88, "美国文学重要作品。", "hym-b036"),
        new SeedBook("海底两万里", "儒勒·凡尔纳", "外国名著", 49.00m, 86, "科幻冒险文学代表作。", "hym-b037"),
        new SeedBook("神秘岛", "儒勒·凡尔纳", "外国名著", 52.00m, 84, "凡尔纳冒险三部曲之一。", "hym-b038"),
        new SeedBook("堂吉诃德", "米格尔·德·塞万提斯", "外国名著", 72.00m, 82, "近代小说奠基性作品。", "hym-b039"),
        new SeedBook("哈姆雷特", "威廉·莎士比亚", "戏剧名著", 36.00m, 80, "莎士比亚四大悲剧之一。", "hym-b040"),
        new SeedBook("罗密欧与朱丽叶", "威廉·莎士比亚", "戏剧名著", 35.00m, 78, "世界爱情悲剧经典。", "hym-b041"),
        new SeedBook("麦克白", "威廉·莎士比亚", "戏剧名著", 34.00m, 76, "权力与欲望主题悲剧。", "hym-b042"),
        new SeedBook("红与黑", "司汤达", "外国名著", 48.00m, 74, "法国现实主义文学名篇。", "hym-b043"),
        new SeedBook("高老头", "奥诺雷·德·巴尔扎克", "外国名著", 44.00m, 72, "人间喜剧核心作品之一。", "hym-b044"),
        new SeedBook("欧也妮葛朗台", "奥诺雷·德·巴尔扎克", "外国名著", 40.00m, 70, "批判资本与人性的经典小说。", "hym-b045"),
        new SeedBook("包法利夫人", "古斯塔夫·福楼拜", "外国名著", 46.00m, 68, "现实主义小说经典。", "hym-b046"),
        new SeedBook("追忆似水年华", "马塞尔·普鲁斯特", "外国名著", 128.00m, 66, "意识流文学里程碑。", "hym-b047"),
        new SeedBook("尤利西斯", "詹姆斯·乔伊斯", "外国名著", 99.00m, 64, "现代主义文学代表作。", "hym-b048"),
        new SeedBook("荷马史诗", "荷马", "史诗名著", 118.00m, 62, "西方文学源头性史诗。", "hym-b049"),
        new SeedBook("神曲", "但丁·阿利吉耶里", "史诗名著", 88.00m, 60, "中世纪文学巅峰之作。", "hym-b050")
    ];
}
