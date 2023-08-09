use std::{env, path::Path};

use index::*;
use types::Position;

mod index;
mod parser;
mod printer;
mod tuner;
mod types;

const EPOCHS: usize = 300;
const K: f32 = 1.5;

fn main() {
    let mut positions = load_positions_from_args();
    let mut weights = get_weights();

    tuner::tune(&mut positions, &mut weights, EPOCHS, K);
    printer::print(&weights);
}

fn load_positions_from_args() -> Vec<Position> {
    let mut positions = vec![];
    for path in env::args().skip(1) {
        load_positions(path, &mut positions);
    }
    positions
}

fn load_positions<P>(path: P, output: &mut Vec<Position>)
where
    P: AsRef<Path> + std::fmt::Display,
{
    println!("Preparing positions from {}...", path);

    let extension = path.as_ref().extension().unwrap();
    let parser_fn = match extension.to_str().unwrap() {
        "epd" => parser::epd_parser,
        "book" => parser::book_parser,
        _ => panic!("Invalid file type: {}", extension.to_str().unwrap()),
    };

    let parsed_positions = parser::parse_file(path.as_ref(), parser_fn)
        .into_iter()
        .map(|(board, label)| Position::new(board, label));

    output.extend(parsed_positions);
}

fn get_weights() -> Vec<f32> {
    let mut weights = vec![10.0; SIZE_FEATURES];
    for (piece, value) in [100.0, 300.0, 325.0, 500.0, 900.0].iter().enumerate() {
        weights[IDX_MATERIAL_MG + piece] = *value;
        weights[IDX_MATERIAL_EG + piece] = *value;
    }
    weights
}

#[allow(dead_code)]
fn get_optimal_k(positions: &[Position], weights: &[f32]) -> f32 {
    println!("Finding optimal k...");
    let k = tuner::find_optimal_k(positions, weights);
    println!("Optimal k: {}", k);
    k
}
