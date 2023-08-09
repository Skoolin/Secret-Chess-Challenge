use std::env;

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
    let args = env::args();

    let mut positions = vec![];
    for path in args.skip(1) {
        if path.contains("book") {
            load_book_positions(path, &mut positions);
        } else {
            load_positions(path, &mut positions);
        }
    }

    let mut weights = get_weights();

    tuner::tune(&mut positions, &mut weights, EPOCHS, K);
    printer::print(&weights);
}

fn load_book_positions(path: String, positions: &mut Vec<Position>) {
    println!("Preparing positions from {}...", path);
    parser::parse_book_file(&path)
        .into_iter()
        .map(|(board, label)| Position::new(board, label))
        .for_each(|position| positions.push(position));
}

fn load_positions(path: String, positions: &mut Vec<Position>) {
    println!("Preparing positions from {}...", path);
    parser::parse_epd_file(&path)
        .into_iter()
        .map(|(board, label)| Position::new(board, label))
        .for_each(|position| positions.push(position));
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
