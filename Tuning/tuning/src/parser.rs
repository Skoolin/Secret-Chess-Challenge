use std::{fs::read_to_string, path::Path};

use chess::*;

/// Parses a file and returns a vector of boards and labels.
pub fn parse_file<T>(path: &Path, parser: T) -> Vec<(Board, f32)>
where
    T: Fn(&str) -> (Board, f32),
{
    read_to_string(path).unwrap().lines().map(parser).collect()
}

/// `r1b2rk1/1pq2pbp/3p1np1/2p1n3/NPP1p3/P3P3/1BQPBPPP/R1N2RK1 b - - c9 "1-0";`
pub fn epd_parser(line: &str) -> (Board, f32) {
    let (fen, label) = line.split_once(" c9 ").unwrap();
    let label = match label {
        "\"1-0\";" => 1.0,
        "\"0-1\";" => 0.0,
        "\"1/2-1/2\";" => 0.5,
        _ => panic!("Invalid label: '{}'", label),
    };
    (Board::new(fen), label)
}

/// `1r4k1/6p1/7p/4p3/R7/3rPNP1/1b3P1P/5RK1 b - - 0 1 [1.0]`
pub fn book_parser(line: &str) -> (Board, f32) {
    let (fen, label) = line.rsplit_once(' ').unwrap();
    let label = match label {
        "[1.0]" => 1.0,
        "[0.0]" => 0.0,
        "[0.5]" => 0.5,
        _ => panic!("Invalid label: '{}'", label),
    };
    (Board::new(fen), label)
}
