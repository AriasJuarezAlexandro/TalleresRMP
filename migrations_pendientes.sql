-- Esquema aplicado en Turso (turso db shell talleresrmp-ariasjuarezalexandro).
-- Todo lo de este archivo ya está ejecutado en producción; se conserva como
-- referencia del esquema actual y para poder recrear la base desde cero
-- (por ejemplo, en un entorno nuevo).

DROP TABLE IF EXISTS MantenimientoProducto;
DROP TABLE IF EXISTS Mantenimiento;

CREATE TABLE Mantenimiento (
    IdMantenimiento  INTEGER PRIMARY KEY AUTOINCREMENT,
    Numero           TEXT    NOT NULL,
    Cliente          TEXT    NOT NULL,
    Telefono         TEXT    NOT NULL,
    Marca            TEXT    NOT NULL,
    Modelo           TEXT    NOT NULL,
    Placa            TEXT    NOT NULL,
    KM               TEXT    NOT NULL,
    Total            REAL    NOT NULL DEFAULT 0,
    FechaCreacion    TEXT    NOT NULL,
    Fotos            TEXT,
    Descripcion      TEXT,
    Precio           REAL
);

CREATE TABLE MantenimientoProducto (
    IdMantenimientoProducto  INTEGER PRIMARY KEY AUTOINCREMENT,
    IdMantenimiento          INTEGER NOT NULL REFERENCES Mantenimiento(IdMantenimiento),
    Item                     TEXT    NOT NULL,
    Cantidad                 INTEGER NOT NULL DEFAULT 1,
    Descripcion              TEXT    NOT NULL,
    PrecioUnitario           REAL    NOT NULL DEFAULT 0,
    Importe                  REAL    NOT NULL DEFAULT 0,
    EsServicio               INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IX_Mantenimiento_FechaCreacion ON Mantenimiento(FechaCreacion);
CREATE INDEX IX_MantenimientoProducto_IdMantenimiento ON MantenimientoProducto(IdMantenimiento);

-- ============================================================
-- Tabla Usuario (login simple, sin librerías de auth)
-- ============================================================
DROP TABLE IF EXISTS Usuario;

CREATE TABLE Usuario (
    IdUsuario       INTEGER PRIMARY KEY AUTOINCREMENT,
    Login           TEXT    NOT NULL UNIQUE,
    Password        TEXT    NOT NULL,           -- hash PBKDF2, nunca texto plano
    Nivel           TEXT    NOT NULL DEFAULT 'C' CHECK (Nivel IN ('A','B','C')),
    FechaCreacion   TEXT    NOT NULL DEFAULT (datetime('now')),
    Activo          INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IX_Usuario_Login ON Usuario(Login);

-- Usuario admin inicial (login: admin / password: admin123, nivel A).
-- Generado con Services/PasswordHasher.Hash("admin123") -- CAMBIAR la contraseña
-- después del primer login.
INSERT INTO Usuario (Login, Password, Nivel) VALUES (
    'admin',
    '100000.DLvDq2+t1eg14mSvOoFv0g==.OMtJeYPpXd+YybbtumMZSzorOf8cG+yKDoNt3k81usQ=',
    'A'
);
