use chess::*;

/// Parses an EPD file and returns a vector of boards and labels.
pub fn parse_epd_file(path: &str) -> Vec<(Board, f32)> {
    let mut positions = vec![];
    let lines = std::fs::read_to_string(path).unwrap();
    for line in lines.lines() {
        let (fen, label) = line.split_once(" c9 ").unwrap();
        positions.push((Board::new(fen), parse_label(label)));
    }
    positions
}

pub fn parse_book_file(path: &str) -> Vec<(Board, f32)> {
    let mut positions = vec![];
    let lines = std::fs::read_to_string(path).unwrap();
    for line in lines.lines() {
        let fen = line.match_indices(' ').nth(4).map(|(index, _)| line.split_at(index)).unwrap().0;
        let label = parse_book_label(line.rsplit_once(' ').unwrap().1);
        positions.push((Board::new(fen), label));
    }
    positions
}

fn parse_book_label(label: &str) -> f32 {
    match label {
        "[1.0]" => 1.0,
        "[0.0]" => 0.0,
        "[0.5]" => 0.5,
        _ => panic!("Invalid label: '{}'", label),
    }
}

fn parse_label(label: &str) -> f32 {
    match label {
        "\"1-0\";" => 1.0,
        "\"0-1\";" => 0.0,
        "\"1/2-1/2\";" => 0.5,
        _ => panic!("Invalid label: '{}'", label),
    }
}
