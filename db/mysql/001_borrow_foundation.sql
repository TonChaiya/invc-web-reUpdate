-- INVC Web — application-owned MySQL database `invc_web` — migration 001: Borrow foundation (mirror of INV type-09 receipts).
-- Target: MySQL 8.x database `invc_web` ONLY. This file must never be executed against SQL Server INV.
-- Applied by scripts/mysql-migrate.ps1 (records this version in schema_version). Idempotent: uses IF NOT EXISTS, never drops.
-- Column widths follow the LIVE INV schema (RECEIVE_NO nvarchar(10), INVOICE_NO nvarchar(20), DPT_CODE nvarchar(6),
-- WORKING_CODE nvarchar(7), LOTNO nvarchar(20), MANUFAC_CODE/VENDORC nvarchar(6)) with headroom; see docs/borrow-data-map.md.

CREATE TABLE IF NOT EXISTS schema_version (
    version      INT          NOT NULL PRIMARY KEY,
    description  VARCHAR(200) NOT NULL,
    applied_at   DATETIME     NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Mirror of INV OTH_IVO rows with RTRIM(RCV_TYPE) = '09'. Primary key = OTH_IVO.RECORD_NUMBER.
-- UNIQUE(receive_no): proven safe on live data 2026-09-17 — RECEIVE_NO is unique across all 110 headers (docs/borrow-data-map.md).
CREATE TABLE IF NOT EXISTS borrow_source_bill (
    source_record_number  INT          NOT NULL PRIMARY KEY,
    receive_no            VARCHAR(20)  NOT NULL,
    invoice_no            VARCHAR(40)  NULL,
    invoice_date          DATETIME     NULL,
    date_receive          DATETIME     NULL,
    facility_code         VARCHAR(12)  NULL,
    facility_name         VARCHAR(255) NULL,
    source_sysdate        DATETIME     NULL,
    source_hash           CHAR(64)     NOT NULL,
    last_synced_at        DATETIME     NOT NULL,
    UNIQUE KEY ux_borrow_source_bill_receive_no (receive_no),
    KEY ix_borrow_source_bill_facility (facility_code),
    KEY ix_borrow_source_bill_date_receive (date_receive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Mirror of INV OTH_IVOC rows belonging to a type-09 header (joined on RECEIVE_NO). Primary key = OTH_IVOC.RECORD_NUMBER.
CREATE TABLE IF NOT EXISTS borrow_source_item (
    source_record_number       INT            NOT NULL PRIMARY KEY,
    source_bill_record_number  INT            NOT NULL,
    receive_no                 VARCHAR(20)    NOT NULL,
    working_code               VARCHAR(14)    NOT NULL,
    drug_name                  VARCHAR(255)   NULL,
    qty_order                  DECIMAL(14,0)  NULL,
    pack_ratio                 DECIMAL(7,0)   NULL,
    lot_no                     VARCHAR(40)    NULL,
    manufac_code               VARCHAR(12)    NULL,
    vendor_code                VARCHAR(12)    NULL,
    source_hash                CHAR(64)       NOT NULL,
    last_synced_at             DATETIME       NOT NULL,
    KEY ix_borrow_source_item_receive_no (receive_no),
    KEY ix_borrow_source_item_working_code (working_code),
    KEY ix_borrow_source_item_bill (source_bill_record_number),
    CONSTRAINT fk_borrow_source_item_bill FOREIGN KEY (source_bill_record_number)
        REFERENCES borrow_source_bill (source_record_number) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO schema_version (version, description, applied_at)
SELECT 1, 'borrow foundation: borrow_source_bill, borrow_source_item', NOW()
WHERE NOT EXISTS (SELECT 1 FROM schema_version WHERE version = 1);
