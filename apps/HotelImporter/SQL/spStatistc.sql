USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_Statistics]    Script Date: 13/05/2026 20:34:50 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[sp_Import_Hotel_Statistics]
    @FileName VARCHAR(100),
    @XmlData XML
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- MODO ACUMULATIVO:
        -- No borramos nada. Insertamos directamente lo que venga en el XML.
        
        INSERT INTO [dbo].[Hotel_Statistics] (
            [FileName],
            [LoadDate],
            [ProcessStatus],
            [Status],        -- Default 0 (False)
            [HotelCode],     -- Atributo Raíz
            [ReportDate],    -- Atributo Raíz
            [MarketCode],
            [RoomClass],
            [RoomType],
            [Rooms],
            [Persons],
            [NoShowRooms],
            [CancelRooms]
        )
        SELECT 
            @FileName,
            GETDATE(),
            1, -- ProcessStatus: 1 (Procesado)
            0, -- Status: 0
            
            -- Navegamos hacia arriba (../) para buscar los atributos del nodo padre <statistics>
            T.c.value('../@hotel_code', 'varchar(20)'),
            T.c.value('../@date', 'date'),

            -- Mapeo de nodos hijos
            T.c.value('(market_code)[1]', 'varchar(10)'),
            T.c.value('(room_class)[1]', 'varchar(20)'),
            T.c.value('(room_type)[1]', 'varchar(20)'),
            T.c.value('(rooms)[1]', 'int'),
            T.c.value('(persons)[1]', 'int'),
            T.c.value('(noshow_rooms)[1]', 'int'),
            T.c.value('(cancel_rooms)[1]', 'int')
        FROM 
            @XmlData.nodes('/statistics/statistic_record') AS T(c);

        -- Retornamos éxito y cantidad de filas insertadas
        SELECT 'SUCCESS' as Result, @@ROWCOUNT as RowsInserted;

    END TRY
    BEGIN CATCH
        -- Manejo de errores
        SELECT 
            'ERROR' as Result, 
            ERROR_MESSAGE() as ErrorMessage;
    END CATCH
END
