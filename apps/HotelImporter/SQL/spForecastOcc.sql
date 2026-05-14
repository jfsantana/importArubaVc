USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_ForecastOcc]    Script Date: 13/05/2026 20:33:59 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[sp_Import_Hotel_ForecastOcc]
    @FileName VARCHAR(100),
    @XmlData XML
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO [dbo].[Hotel_ForecastOcc] (
            [SORT_ORDER],[BUSINESS_DATE],[NO_DEFINITE_ROOMS],
            [MARKET_CODE],[ROOM_CATEGORY],[NO_OF_ROOMS1],[LOGO]
        )
        SELECT
            D.d.value('(SORT_ORDER)[1]',       'int'),
            TRY_CONVERT(date, D.d.value('(BUSINESS_DATE)[1]', 'varchar(20)'), 10),
            R.r.value('(NO_DEFINITE_ROOMS)[1]',              'int'),
            R.r.value('(MARKET_CODE)[1]',                    'varchar(10)'),
            R.r.value('(ROOM_CATEGORY)[1]',                  'varchar(50)'),
            R.r.value('(LIST_DETAIL/DETAIL/NO_OF_ROOMS1)[1]','int'),
            0
        FROM @XmlData.nodes('/DETAIL_AVAIL/LIST_G_5/G_5/LIST_DAY/DAY') AS D(d)
        CROSS APPLY D.d.nodes('LIST_ROOM_TYPE/ROOM_TYPE') AS R(r)
        WHERE
            R.r.value('(ROOM_CATEGORY)[1]', 'varchar(50)') = 'Total Occupied'
            AND R.r.value('(MARKET_CODE)[1]', 'varchar(10)') = 'Total'
            AND TRY_CONVERT(date, D.d.value('(BUSINESS_DATE)[1]', 'varchar(20)'), 10) IS NOT NULL;

        SELECT 'SUCCESS' AS Result, @@ROWCOUNT AS RowsInserted;
    END TRY
    BEGIN CATCH
        SELECT 'ERROR' AS Result, ERROR_MESSAGE() AS ErrorMessage;
    END CATCH
END