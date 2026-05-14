USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_Customers]    Script Date: 13/05/2026 20:33:33 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[sp_Import_Hotel_Customers]
    @FileName VARCHAR(100),
    @XmlData XML
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- MODO ACUMULATIVO (Append Only)
        
        INSERT INTO [dbo].[Hotel_Customers] (
            [FileName],
            [LoadDate],
            [ProcessStatus],
            [Status],        -- Default 0
            
            -- Encabezado
            [HotelCode],
            [ReportDate],
            
            -- Datos del Cliente
            [InternalID],
            [CustomerType],
            [Name],
            [LegalName],
            [PassportNo],
            [Nationality],
            
            -- Address Info (Aplanado)
            [Address1],
            [City],
            [ZipCode],
            [State],
            [CountryCode],
            [CountryName],
            
            -- Communication Info (Aplanado)
            [Email],
            [Phone]
        )
        SELECT 
            @FileName,
            GETDATE(),
            1, -- ProcessStatus: 1 (Procesado)
            0, -- Status: 0
            
            -- Atributos del nodo raíz (subimos nivel con ../)
            T.c.value('../@hotel_code', 'varchar(20)'),
            T.c.value('../@date', 'date'),

            -- Atributos directos del cliente
            T.c.value('@internal_id', 'varchar(50)'),
            T.c.value('(customer_type)[1]', 'varchar(20)'),
            T.c.value('(name)[1]', 'varchar(255)'),
            T.c.value('(legal_name)[1]', 'varchar(255)'),
            T.c.value('(passport_no)[1]', 'varchar(50)'),
            T.c.value('(nationality)[1]', 'varchar(10)'),

            -- Navegación a nodos hijos (Address Info)
            T.c.value('(address_info/address1)[1]', 'varchar(255)'),
            T.c.value('(address_info/city)[1]', 'varchar(100)'),
            T.c.value('(address_info/zip_code)[1]', 'varchar(20)'),
            T.c.value('(address_info/state)[1]', 'varchar(50)'),
            T.c.value('(address_info/country)[1]', 'varchar(10)'),
            T.c.value('(address_info/country_name)[1]', 'varchar(100)'),

            -- Navegación a nodos hijos (Communication Info)
            T.c.value('(communication_info/email)[1]', 'varchar(255)'),
            T.c.value('(communication_info/phone)[1]', 'varchar(50)')

        FROM 
            @XmlData.nodes('/customers/customer') AS T(c);

        SELECT 'SUCCESS' as Result, @@ROWCOUNT as RowsInserted;

    END TRY
    BEGIN CATCH
        SELECT 
            'ERROR' as Result, 
            ERROR_MESSAGE() as ErrorMessage;
    END CATCH
END
