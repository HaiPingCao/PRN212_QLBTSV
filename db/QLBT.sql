USE master;
GO

IF DB_ID('PRN212_P_QLBT') IS NOT NULL
BEGIN
    ALTER DATABASE PRN212_P_QLBT SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PRN212_P_QLBT;
END
GO

CREATE DATABASE PRN212_P_QLBT;
GO

USE PRN212_P_QLBT;
GO


CREATE TABLE roles (
    id      INT          PRIMARY KEY,
    name    VARCHAR(20)  NOT NULL UNIQUE
);

CREATE TABLE users (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    username        VARCHAR(20)   NOT NULL UNIQUE,  -- MSSV / MSGV
    full_name       NVARCHAR(100) NOT NULL,
    email           VARCHAR(100)  NOT NULL UNIQUE,
    password_hash   VARCHAR(255)  NOT NULL,         -- BCrypt
    role_id         INT           NOT NULL,

    CONSTRAINT fk_users_roles
        FOREIGN KEY (role_id)
        REFERENCES roles(id)
);

CREATE TABLE semesters (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    name            NVARCHAR(20) NOT NULL UNIQUE,
    start_date      DATE NOT NULL,
    end_date        DATE NOT NULL,

    CONSTRAINT chk_semesters_dates
        CHECK (end_date > start_date)
);

CREATE TABLE classes (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    teacher_id      INT           NOT NULL,         -- users.id, role Teacher
    name            NVARCHAR(50)  NOT NULL,
    semester_id     INT           NOT NULL,
    major           NVARCHAR(100) NOT NULL,

    CONSTRAINT fk_classes_users
        FOREIGN KEY (teacher_id)
        REFERENCES users(id),

    CONSTRAINT fk_classes_semesters
        FOREIGN KEY (semester_id)
        REFERENCES semesters(id),

    CONSTRAINT uq_classes
        UNIQUE (
            name,
            semester_id,
            major
        )
);

CREATE TABLE class_students (
    class_id            INT         NOT NULL,
    student_id          INT         NOT NULL,       -- users.id, role Student
    is_active           BIT         NOT NULL DEFAULT 1,   -- 0 = suspended
    joined_at           DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    left_at             DATETIME    NULL,

    PRIMARY KEY (class_id, student_id),

    CONSTRAINT fk_class_students_classes
        FOREIGN KEY (class_id)
        REFERENCES classes(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_class_students_users
        FOREIGN KEY (student_id)
        REFERENCES users(id)
        ON DELETE NO ACTION
);

CREATE TABLE assignments (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    class_id            INT           NOT NULL,
    title               NVARCHAR(255) NOT NULL,
    description         NVARCHAR(MAX),
    file_name           NVARCHAR(255),
    file_path           NVARCHAR(500),
    due_date            DATETIME      NOT NULL,
    allow_late          BIT           NOT NULL DEFAULT 0,
    is_closed           BIT           NOT NULL DEFAULT 0,
    created_at          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT uq_assignments_class
        UNIQUE (id, class_id),

    CONSTRAINT fk_assignments_classes
        FOREIGN KEY (class_id)
        REFERENCES classes(id)
        ON DELETE CASCADE
);

CREATE TABLE submissions (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    assignment_id       INT           NOT NULL,
    class_id            INT           NOT NULL,
    student_id          INT           NOT NULL,
    file_name           NVARCHAR(255) NOT NULL,
    file_path           NVARCHAR(500) NOT NULL,
    submit_count        INT           NOT NULL DEFAULT 1,
    submitted_at        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    score               DECIMAL(4,2)  NULL,
    comment             NVARCHAR(MAX) NULL,
    is_late             BIT           NOT NULL DEFAULT 0,

    CONSTRAINT uq_submissions
        UNIQUE (assignment_id, student_id),

    CONSTRAINT chk_submissions_submit_count
        CHECK (submit_count >= 1),

    CONSTRAINT chk_submissions_score
        CHECK (score IS NULL OR (score >= 0 AND score <= 10)),

    CONSTRAINT fk_submissions_assignments
        FOREIGN KEY (assignment_id, class_id)
        REFERENCES assignments(id, class_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_submissions_class_students
        FOREIGN KEY (class_id, student_id)
        REFERENCES class_students(class_id, student_id)
        ON DELETE NO ACTION
);

-- =============================================
-- SEED
-- =============================================

INSERT INTO roles (id, name) VALUES
    (1, 'Teacher'),
    (2, 'Student');