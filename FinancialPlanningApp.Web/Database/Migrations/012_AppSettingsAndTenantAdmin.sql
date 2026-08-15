CREATE TABLE IF NOT EXISTS app_settings (
    `key` VARCHAR(128) NOT NULL PRIMARY KEY,
    `value` VARCHAR(2048) NULL,
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO app_settings(`key`, `value`)
SELECT 'allow_self_registration', 'true'
WHERE NOT EXISTS (
    SELECT 1 FROM app_settings WHERE `key` = 'allow_self_registration'
);
