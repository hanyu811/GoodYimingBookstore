$connString = "Server=LAPTOP-BRBCVK9S\MSSQLSERVER1;Database=bookstore;Trusted_Connection=True;TrustServerCertificate=True;"
$titles = @(
    "红楼梦", "西游记", "三国演义", "水浒传", "朝花夕拾", "呐喊", "边城", "骆驼祥子", "围城", "平凡的世界",
    "活着", "百年孤独", "1984", "动物农场", "小王子", "老人与海", "悲惨世界", "巴黎圣母院", "战争与和平", "安娜卡列尼娜",
    "复活", "罪与罚", "卡拉马佐夫兄弟", "变形记", "局外人", "月亮与六便士", "傲慢与偏见", "简爱", "呼啸山庄", "雾都孤儿",
    "双城记", "大卫科波菲尔", "鲁滨逊漂流记", "格列佛游记", "汤姆索亚历险记", "哈克贝利费恩历险记", "海底两万里", "神秘岛", "堂吉诃德", "哈姆雷特",
    "罗密欧与朱丽叶", "麦克白", "红与黑", "高老头", "欧也妮葛朗台", "包法利夫人", "追忆似水年华", "尤利西斯", "荷马史诗", "神曲"
)
try {
    $beforeCount = (Invoke-Sqlcmd -ConnectionString $connString -Query "SELECT COUNT(*) as cnt FROM dbo.Books").cnt
    $sql = "BEGIN TRANSACTION; DELETE FROM dbo.Carts WHERE BookId IN (SELECT Id FROM dbo.Books); DELETE FROM dbo.OrderItems WHERE BookId IN (SELECT Id FROM dbo.Books); DELETE FROM dbo.Books; DBCC CHECKIDENT('dbo.Books', RESEED, 0); "
    for ($i = 0; $i -lt $titles.Length; $i++) {
        $title = $titles[$i]; $seedIdx = ($i + 1).ToString("D3"); $imgUrl = "https://picsum.photos/seed/hym-b$seedIdx/320/420";
        $sql += "INSERT INTO dbo.Books (Title, Author, Category, Price, Stock, Description, CoverUrl, ImageUrl, IsListed, CreatedAt) VALUES (N'$title', 'Author', 'Classics', 50.0, 100, 'Description', '$imgUrl', '$imgUrl', 1, GETDATE()); "
    }
    $sql += "COMMIT;"
    Invoke-Sqlcmd -ConnectionString $connString -Query $sql
    $afterCount = (Invoke-Sqlcmd -ConnectionString $connString -Query "SELECT COUNT(*) as cnt FROM dbo.Books").cnt
    $first5 = Invoke-Sqlcmd -ConnectionString $connString -Query "SELECT TOP 5 Id, Title FROM dbo.Books ORDER BY Id ASC"
    $last3 = Invoke-Sqlcmd -ConnectionString $connString -Query "SELECT TOP 3 Id, Title FROM dbo.Books ORDER BY Id DESC"
    $listedCount = (Invoke-Sqlcmd -ConnectionString $connString -Query "SELECT COUNT(*) as cnt FROM dbo.Books WHERE IsListed = 1").cnt
    Write-Output "Before Count: $beforeCount"
    Write-Output "After Count: $afterCount"
    Write-Output "First 5 rows (Id, Title):"
    $first5 | ForEach-Object { "$($_.Id): $($_.Title)" }
    Write-Output "Last 3 rows (Id, Title):"
    ($last3 | Sort-Object Id) | ForEach-Object { "$($_.Id): $($_.Title)" }
    Write-Output "Total Count = 50: $($afterCount -eq 50)"
    Write-Output "IsListed=1 Count = 50: $($listedCount -eq 50)"
} catch { Write-Error $_ }
