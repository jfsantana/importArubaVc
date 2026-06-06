USE [importaruba]
GO
/****** Object:  StoredProcedure [dbo].[sp_Import_Hotel_FutureOccupancy]    Script Date: 05/06/2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================================================
-- Autor:        HotelImporter
-- Descripción:  Importa el reporte de ocupación futura (RESFUTUREOCCUPANCY)
--               generado por Oracle Reports.
--
-- Origen XML:   resfutureoccupancy*****.xml
-- Estructura:
--   /RESFUTUREOCCUPANCY
--     /LIST_G_RESV_TYPE
--       /G_RESV_TYPE      <-- Encabezado: SORT_COLUMN y RESV_TYPE
--         /LIST_G_CONSIDERED_DATE
--           /G_CONSIDERED_DATE  <-- Cada fila a insertar (un día)
--
-- Destino:      [dbo].[Hotel_FutureOccupancy]
--
-- Formatos de fecha en el XML (Oracle):
--   D_DATE          = 'DD-MON-YY' (ej. '01-JUN-26') -> TRY_PARSE con cultura en-US
--   CONSIDERED_DATE = 'MM-DD-YY'  (ej. '06-01-26')  -> TRY_CONVERT estilo 10
-- =============================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_Import_Hotel_FutureOccupancy]
    @FileName VARCHAR(100),
    @XmlData  XML
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- MODO ACUMULATIVO (Append Only): se inserta lo que venga en el XML.

        INSERT INTO [dbo].[Hotel_FutureOccupancy] (
            [SORT_COLUMN],
            [RESV_TYPE],
            [D_DATE],
            [CONSIDERED_DATE],
            [DAY_DESC],
            [ADULTS],
            [CHILDREN],
            [CHILDREN1],
            [CHILDREN2],
            [CHILDREN3],
            [CHILDREN4],
            [CHILDREN5],
            [GUESTS],
            [ARRIVAL_ROOMS],
            [DEPARTURE_ROOMS],
            [DEFINITE_ROOMS],
            [TENTATIVE_ROOMS],
            [OUT_OF_SERVICE],
            [OUT_OF_ORDER],
            [NET_ROOM],
            [PKG_REV],
            [AVG_RROOM_EV],
            [DAY_PICKUP],
            [DAY_REMAIN],
            [PER_DEF_OCC],
            [PER_TENT_OCC],
            [FECHA_CARGA]
        )
        SELECT
            -- Datos del encabezado (G_RESV_TYPE)
            H.h.value('(SORT_COLUMN)[1]',     'int'),
            H.h.value('(RESV_TYPE)[1]',       'varchar(100)'),

            -- Fechas: convertidas con la cultura/estilo apropiados
            TRY_PARSE  (D.d.value('(D_DATE)[1]',          'varchar(20)') AS date USING 'en-US'),
            TRY_CONVERT(date, D.d.value('(CONSIDERED_DATE)[1]', 'varchar(20)'), 10),

            D.d.value('(DAY_DESC)[1]',        'varchar(10)'),
            D.d.value('(ADULTS)[1]',          'int'),
            D.d.value('(CHILDREN)[1]',        'int'),
            D.d.value('(CHILDREN1)[1]',       'int'),
            D.d.value('(CHILDREN2)[1]',       'int'),
            D.d.value('(CHILDREN3)[1]',       'int'),
            D.d.value('(CHILDREN4)[1]',       'int'),
            D.d.value('(CHILDREN5)[1]',       'int'),
            D.d.value('(GUESTS)[1]',          'int'),
            D.d.value('(ARRIVAL_ROOMS)[1]',   'int'),
            D.d.value('(DEPARTURE_ROOMS)[1]', 'int'),
            D.d.value('(DEFINITE_ROOMS)[1]',  'int'),
            D.d.value('(TENTATIVE_ROOMS)[1]', 'int'),
            D.d.value('(OUT_OF_SERVICE)[1]',  'int'),
            D.d.value('(OUT_OF_ORDER)[1]',    'int'),
            D.d.value('(NET_ROOM)[1]',        'decimal(18,4)'),
            D.d.value('(PKG_REV)[1]',         'decimal(18,4)'),
            D.d.value('(AVG_RROOM_EV)[1]',    'decimal(18,6)'),
            D.d.value('(DAY_PICKUP)[1]',      'int'),
            D.d.value('(DAY_REMAIN)[1]',      'int'),
            D.d.value('(PER_DEF_OCC)[1]',     'decimal(18,6)'),
            D.d.value('(PER_TENT_OCC)[1]',    'decimal(18,6)'),
            GETDATE()                          -- FECHA_CARGA
        FROM @XmlData.nodes('/RESFUTUREOCCUPANCY/LIST_G_RESV_TYPE/G_RESV_TYPE') AS H(h)
        CROSS APPLY H.h.nodes('LIST_G_CONSIDERED_DATE/G_CONSIDERED_DATE') AS D(d);

        SELECT 'SUCCESS' AS Result, @@ROWCOUNT AS RowsInserted;
    END TRY
    BEGIN CATCH
        SELECT 'ERROR' AS Result, ERROR_MESSAGE() AS ErrorMessage;
    END CATCH
END
GO
