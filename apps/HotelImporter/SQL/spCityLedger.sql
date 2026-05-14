USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_CityLedger]    Script Date: 13/05/2026 20:33:00 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[sp_Import_Hotel_CityLedger]
    @FileName VARCHAR(100),
    @XmlData XML
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- MODO ACUMULATIVO (Append Only)
        
        INSERT INTO [dbo].[Hotel_CityLedger] (
            [FileName],
            [LoadDate],
            [ProcessStatus],
            [Status],        -- Default 0 (False)
            
            -- Encabezado Global
            [HotelCode],
            [ReportDate],
            
            -- Transacción
            [TrxCode],
            [TransactionType],
            [BillNo],
            [InvoiceNo],
            [Amount],
            [CreditCardLast4],
            
            -- Account Info (Aplanado)
            [AccountCustomerID],
            [AccountName],
            [AccountNumber],
            
            -- Reservation Info (Aplanado)
            [ConfirmationNo],
            [ArrivalDate],
            [DepartureDate],
            [GuestName]
        )
        SELECT 
            @FileName,
            GETDATE(),
            1, -- ProcessStatus: 1 (Procesado)
            0, -- Status: 0
            
            -- Atributos del nodo Raíz (../)
            T.c.value('../@hotel_code', 'varchar(20)'),
            T.c.value('../@date', 'date'),

            -- Atributos de la Transacción
            T.c.value('@trx_code', 'varchar(20)'),
            T.c.value('@transaction_type', 'varchar(50)'),
            
            -- Elementos Hijos directos
            T.c.value('(bill_no)[1]', 'varchar(50)'),
            T.c.value('(invoice_no)[1]', 'varchar(50)'),
            T.c.value('(amount)[1]', 'decimal(18,2)'),
            T.c.value('(credit_card_last_4_digits)[1]', 'varchar(10)'),

            -- Navegación a Account Info
            T.c.value('(account_info/@customer_internal_id)[1]', 'varchar(50)'), -- Ojo: es un atributo dentro del nodo hijo
            T.c.value('(account_info/account_name)[1]', 'varchar(255)'),
            T.c.value('(account_info/account_number)[1]', 'varchar(50)'),

            -- Navegación a Reservation Info
            T.c.value('(reservation_info/confirmation_no)[1]', 'varchar(50)'),
            T.c.value('(reservation_info/arrival_date)[1]', 'date'),
            T.c.value('(reservation_info/departure_date)[1]', 'date'),
            T.c.value('(reservation_info/guest_name)[1]', 'varchar(255)')

        FROM 
            @XmlData.nodes('/city_ledger/transaction') AS T(c);

        SELECT 'SUCCESS' as Result, @@ROWCOUNT as RowsInserted;

    END TRY
    BEGIN CATCH
        SELECT 
            'ERROR' as Result, 
            ERROR_MESSAGE() as ErrorMessage;
    END CATCH
END
