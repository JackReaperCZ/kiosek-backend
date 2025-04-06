CREATE DATABASE `kiosek`;

USE `kiosek`;

CREATE TABLE IF NOT EXISTS `admins` (
  `id` INT(11) NOT NULL AUTO_INCREMENT,
  `username` VARCHAR(255) NOT NULL,
  `datum_pridani` DATE NOT NULL DEFAULT (CURDATE()),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `projects` (
  `id` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  `description` LONGTEXT NOT NULL,
  `date` DATE NOT NULL DEFAULT (CURDATE()),
  `status` CHAR(1) NOT NULL DEFAULT 'W',
  `author` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `media` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_media_projects` (`id_pro`),
  CONSTRAINT `FK_media_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `tags` (
  `id` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  `added` BIT(1) NOT NULL DEFAULT b'0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `taged` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `id_tag` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_taged_projects` (`id_pro`),
  KEY `FK_taged_tags` (`id_tag`),
  CONSTRAINT `FK_taged_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT `FK_taged_tags` FOREIGN KEY (`id_tag`) REFERENCES `tags` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `thumbnails` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_thumbnail_projects` (`id_pro`),
  CONSTRAINT `FK_thumbnail_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
