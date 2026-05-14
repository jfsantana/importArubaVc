USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_Revenue]    Script Date: 13/05/2026 20:34:28 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[sp_Import_Hotel_Revenue]
    @FileName VARCHAR(100),
    @XmlData XML
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- MODO ACUMULATIVO (Append Only)
        
        INSERT INTO [dbo].[Hotel_Revenue] (
            [FileName],
            [LoadDate],
            [ProcessStatus],
            [Status],        -- Default 0
            
            -- Encabezado Global
            [HotelCode],
            [ReportDate],
            
            -- Nivel 1: Transaction Total (Padre)
            [HeaderTrxCode],
            [HeaderType],
            [Description],
            [TotalAmount],
            [TotalGuestLedger],
            
            -- Nivel 2: Transaction Detail (Hijo)
            [DetailMarketCode],
            [DetailRoomClass],
            [DetailTrxAmount],
            [DetailGuestLedger],
            [DetailArLedger]
        )
        SELECT 
            @FileName,
            GETDATE(),
            1, -- ProcessStatus: 1 (Procesado)
            0, -- Status: 0
            
            -- Datos Raíz (Subimos desde el padre)
            Header.data.value('../@hotel_code', 'varchar(20)'),
            Header.data.value('../@date', 'date'),

            -- Datos del Padre (Transaction Total)
            Header.data.value('(transaction_code)[1]', 'varchar(20)'),
            Header.data.value('@transaction_type', 'varchar(50)'),
            Header.data.value('(description)[1]', 'varchar(255)'),
            Header.data.value('(total_amount)[1]', 'decimal(18,4)'),
            Header.data.value('(total_guest_ledger)[1]', 'decimal(18,4)'),

            -- Datos del Hijo (Transaction Detail)
            Detail.data.value('(market_code)[1]', 'varchar(10)'),
            Detail.data.value('(room_class)[1]', 'varchar(20)'),
            Detail.data.value('(trx_amount)[1]', 'decimal(18,4)'),
            Detail.data.value('(trx_guest_ledger)[1]', 'decimal(18,4)'),
            Detail.data.value('(trx_ar_ledger)[1]', 'decimal(18,4)') -- Algunos nodos no lo tienen, devolverá NULL, lo cual es correcto

        FROM 
            @XmlData.nodes('/revenue/transaction_total') AS Header(data)
            CROSS APPLY Header.data.nodes('transaction_details/transaction') AS Detail(data);
            -- CROSS APPLY "cruza" cada padre con sus respectivos hijos

        SELECT 'SUCCESS' as Result, @@ROWCOUNT as RowsInserted;

    END TRY
    BEGIN CATCH
        SELECT 
            'ERROR' as Result, 
            ERROR_MESSAGE() as ErrorMessage;
    END CATCH
END
