/*
    Historico de informes Eurolab / WSNutec
    Fecha: 2026-07-06

    Objetivo:
    - Guardar historico regenerable de informes RESUMEN / COMPLETO por NumChrono.
    - Mantener metadata buscable separada del contenido pesado.
    - Controlar procesamiento por batch mediante una cola idempotente.

    Uso:
    - Revisar la base activa antes de ejecutar.
    - Si aplica, descomentar:
        USE DBNutec;
        GO
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
    Tabla principal: metadata ligera para busqueda y control.
    Llave funcional: NumChrono + TipoInforme.
*/
IF OBJECT_ID('dbo.InformeHistorico', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InformeHistorico
    (
        IdInformeHistorico BIGINT IDENTITY(1,1) NOT NULL,
        NumChrono VARCHAR(50) NOT NULL,
        TipoInforme VARCHAR(20) NOT NULL,
        LienJournalEchant VARCHAR(50) NULL,

        CodeClienteFacture VARCHAR(50) NULL,
        LibelleClientFacture VARCHAR(300) NULL,
        CodeProducto VARCHAR(50) NULL,
        LibelleProducto VARCHAR(300) NULL,
        Lote VARCHAR(200) NULL,
        ReferenceExterne VARCHAR(300) NULL,

        FechaRecepcion DATETIME NULL,
        FechaMuestreo DATETIME NULL,
        FechaRealizacionMax DATETIME NULL,
        FechaGeneracion DATETIME NOT NULL CONSTRAINT DF_InformeHistorico_FechaGeneracion DEFAULT (GETDATE()),

        HashContenido VARCHAR(128) NULL,
        Activo BIT NOT NULL CONSTRAINT DF_InformeHistorico_Activo DEFAULT ((1)),

        FecAltaAudit DATETIME NOT NULL CONSTRAINT DF_InformeHistorico_FecAltaAudit DEFAULT (GETDATE()),
        FecActAudit DATETIME NULL,
        UserAltaAudit VARCHAR(50) NULL,
        UserActAudit VARCHAR(50) NULL,

        CONSTRAINT PK_InformeHistorico PRIMARY KEY CLUSTERED (IdInformeHistorico),
        CONSTRAINT UQ_InformeHistorico_NumChrono_TipoInforme UNIQUE (NumChrono, TipoInforme),
        CONSTRAINT CK_InformeHistorico_TipoInforme CHECK (TipoInforme IN ('RESUMEN', 'COMPLETO'))
    );
END
GO

/*
    Tabla de contenido pesado:
    - HTML debe guardarse comprimido con GZip como VARBINARY(MAX).
    - PayloadJson y ParametrosJson quedan visibles como NVARCHAR(MAX) para trazabilidad.
    - PdfBytes es opcional y queda NULL si se decide regenerar PDF desde HTML.
*/
IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InformeHistoricoContenido
    (
        IdInformeHistorico BIGINT NOT NULL,

        HtmlBodyBin VARBINARY(MAX) NOT NULL,
        HtmlHeaderBin VARBINARY(MAX) NULL,
        HtmlFooterBin VARBINARY(MAX) NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        ParametrosJson NVARCHAR(MAX) NULL,
        PdfBytes VARBINARY(MAX) NULL,

        CONSTRAINT PK_InformeHistoricoContenido PRIMARY KEY CLUSTERED (IdInformeHistorico),
        CONSTRAINT FK_InformeHistoricoContenido_InformeHistorico
            FOREIGN KEY (IdInformeHistorico)
            REFERENCES dbo.InformeHistorico (IdInformeHistorico)
    );
END
GO

/*
    Migracion idempotente para bases donde ya existia la version anterior:
    PayloadJsonBin / ParametrosJsonBin comprimidos pasan a columnas visibles.

    Las columnas *Bin no se eliminan automaticamente para evitar perdida accidental.
    Si ya no se necesitan despues de validar la migracion, pueden retirarse en una ventana aparte.
*/
IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJson') IS NULL
BEGIN
    ALTER TABLE dbo.InformeHistoricoContenido
    ADD PayloadJson NVARCHAR(MAX) NULL;
END
GO

IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'ParametrosJson') IS NULL
BEGIN
    ALTER TABLE dbo.InformeHistoricoContenido
    ADD ParametrosJson NVARCHAR(MAX) NULL;
END
GO

/*
    Intenta migrar datos existentes desde GZip binario.
    Si SQL Server no soporta DECOMPRESS o la collation UTF-8 no existe, este bloque no detiene el script.
*/
BEGIN TRY
    IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJsonBin') IS NOT NULL
    BEGIN
        UPDATE dbo.InformeHistoricoContenido
        SET PayloadJson = CONVERT(NVARCHAR(MAX), CONVERT(VARCHAR(MAX), DECOMPRESS(PayloadJsonBin)))
        WHERE PayloadJson IS NULL
          AND PayloadJsonBin IS NOT NULL;
    END

    IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.InformeHistoricoContenido', 'ParametrosJsonBin') IS NOT NULL
    BEGIN
        UPDATE dbo.InformeHistoricoContenido
        SET ParametrosJson = CONVERT(NVARCHAR(MAX), CONVERT(VARCHAR(MAX), DECOMPRESS(ParametrosJsonBin)))
        WHERE ParametrosJson IS NULL
          AND ParametrosJsonBin IS NOT NULL;
    END
END TRY
BEGIN CATCH
    PRINT 'No se pudieron migrar automaticamente PayloadJsonBin/ParametrosJsonBin. Regenerar historicos o migrar desde la aplicacion.';
    PRINT ERROR_MESSAGE();
END CATCH
GO

/*
    Limpieza segura de columnas anteriores.
    Solo se eliminan cuando PayloadJson ya quedo poblado para todos los registros existentes.
*/
IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJson') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.InformeHistoricoContenido
       WHERE PayloadJson IS NULL
   )
BEGIN
    ALTER TABLE dbo.InformeHistoricoContenido
    ALTER COLUMN PayloadJson NVARCHAR(MAX) NOT NULL;
END
GO

IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJsonBin') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJson') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.InformeHistoricoContenido
       WHERE PayloadJson IS NULL
   )
BEGIN
    ALTER TABLE dbo.InformeHistoricoContenido
    DROP COLUMN PayloadJsonBin;
END
GO

IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'ParametrosJsonBin') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'ParametrosJson') IS NOT NULL
BEGIN
    ALTER TABLE dbo.InformeHistoricoContenido
    DROP COLUMN ParametrosJsonBin;
END
GO

IF OBJECT_ID('dbo.InformeHistoricoContenido', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InformeHistoricoContenido', 'PayloadJsonBin') IS NOT NULL
BEGIN
    PRINT 'PayloadJsonBin no se elimino porque existen registros sin PayloadJson migrado.';
END
GO

/*
    Cola de procesamiento batch.
    Estatus:
    - PENDIENTE: listo para procesar
    - PROCESANDO: tomado por una corrida
    - OK: historico insertado o ya existente
    - ERROR: fallo reintentable
    - SIN_DATOS: WSOET no regreso datos para el crono

    LockId / LockedUntil evitan registros atorados si el batch se cae.
*/
IF OBJECT_ID('dbo.InformeHistoricoQueue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InformeHistoricoQueue
    (
        IdQueue BIGINT IDENTITY(1,1) NOT NULL,
        NumChrono VARCHAR(50) NOT NULL,
        TipoInforme VARCHAR(20) NOT NULL,

        Estatus VARCHAR(20) NOT NULL CONSTRAINT DF_InformeHistoricoQueue_Estatus DEFAULT ('PENDIENTE'),
        Intentos INT NOT NULL CONSTRAINT DF_InformeHistoricoQueue_Intentos DEFAULT ((0)),
        ProximoIntento DATETIME NULL,
        UltimoError VARCHAR(MAX) NULL,

        LockId UNIQUEIDENTIFIER NULL,
        LockedUntil DATETIME NULL,

        FechaReferencia DATETIME NULL,
        FechaInicioProceso DATETIME NULL,
        FechaFinProceso DATETIME NULL,

        FecAltaAudit DATETIME NOT NULL CONSTRAINT DF_InformeHistoricoQueue_FecAltaAudit DEFAULT (GETDATE()),
        FecActAudit DATETIME NULL,
        UserAltaAudit VARCHAR(50) NULL,
        UserActAudit VARCHAR(50) NULL,

        CONSTRAINT PK_InformeHistoricoQueue PRIMARY KEY CLUSTERED (IdQueue),
        CONSTRAINT UQ_InformeHistoricoQueue_NumChrono_TipoInforme UNIQUE (NumChrono, TipoInforme),
        CONSTRAINT CK_InformeHistoricoQueue_TipoInforme CHECK (TipoInforme IN ('RESUMEN', 'COMPLETO')),
        CONSTRAINT CK_InformeHistoricoQueue_Estatus CHECK (Estatus IN ('PENDIENTE', 'PROCESANDO', 'OK', 'ERROR', 'SIN_DATOS'))
    );
END
GO

/*
    Indices de busqueda.
*/
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_InformeHistorico_NumChrono'
      AND object_id = OBJECT_ID('dbo.InformeHistorico')
)
BEGIN
    CREATE INDEX IX_InformeHistorico_NumChrono
    ON dbo.InformeHistorico (NumChrono);
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_InformeHistorico_ClienteFechas'
      AND object_id = OBJECT_ID('dbo.InformeHistorico')
)
BEGIN
    CREATE INDEX IX_InformeHistorico_ClienteFechas
    ON dbo.InformeHistorico (CodeClienteFacture, FechaRecepcion, FechaMuestreo);
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_InformeHistoricoQueue_Procesar'
      AND object_id = OBJECT_ID('dbo.InformeHistoricoQueue')
)
BEGIN
    CREATE INDEX IX_InformeHistoricoQueue_Procesar
    ON dbo.InformeHistoricoQueue (Estatus, ProximoIntento, LockedUntil, Intentos);
END
GO

/*
    Consultas de referencia para el batch:

    1) Encolar sin duplicar:

    INSERT INTO dbo.InformeHistoricoQueue (NumChrono, TipoInforme, FechaReferencia, UserAltaAudit)
    SELECT @NumChrono, @TipoInforme, @FechaReferencia, 'batch'
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.InformeHistoricoQueue
        WHERE NumChrono = @NumChrono
          AND TipoInforme = @TipoInforme
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.InformeHistorico
        WHERE NumChrono = @NumChrono
          AND TipoInforme = @TipoInforme
    );

    2) Seleccionar candidatos:

    SELECT TOP (@Take) *
    FROM dbo.InformeHistoricoQueue
    WHERE
        Estatus = 'PENDIENTE'
        OR (Estatus = 'ERROR' AND (ProximoIntento IS NULL OR ProximoIntento <= GETDATE()))
        OR (Estatus = 'PROCESANDO' AND LockedUntil <= GETDATE())
    ORDER BY
        CASE Estatus WHEN 'PENDIENTE' THEN 0 WHEN 'ERROR' THEN 1 ELSE 2 END,
        ISNULL(ProximoIntento, '19000101'),
        IdQueue;

    3) Marcar como procesando:

    UPDATE dbo.InformeHistoricoQueue
    SET Estatus = 'PROCESANDO',
        LockId = @LockId,
        LockedUntil = DATEADD(MINUTE, @LockMinutes, GETDATE()),
        FechaInicioProceso = GETDATE(),
        FecActAudit = GETDATE(),
        UserActAudit = 'batch'
    WHERE IdQueue = @IdQueue;
*/
