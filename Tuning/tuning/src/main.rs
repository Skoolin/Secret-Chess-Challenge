use std::env;

use chess::*;
use index::*;
use types::Position;

mod parser;
mod tuner;
mod types;

fn main() {
    let args = env::args();

    let mut positions = vec![];
    for path in args.skip(1) {
        load_positions(path, &mut positions);
    }

    let mut weights = get_weights();

    let k = get_optimal_k(&positions, &weights);
    tuner::tune(&mut positions, &mut weights, 300, k);

    print_weights(&weights);
}

fn print_weights(weights: &[f32]) {
    println!();
    print_material(weights, IDX_MATERIAL_MG);
    print_material(weights, IDX_MATERIAL_EG);

    println!();
    print_psqt(weights, IDX_PSQT_MG);
    print_psqt(weights, IDX_PSQT_EG);
}

fn print_material(weights: &[f32], start: usize) {
    for piece in 0..5 {
        let index = start + piece;
        print!("{:>3}, ", weights[index].round());
    }
    println!();
}

fn print_psqt(weights: &[f32], start: usize) {
    for piece in 0..6 {
        println!("// {:?}", Piece::from(piece));
        for row in (0..8).rev() {
            for col in 0..8 {
                let square = chess::Square::from_rank_file(row, col);
                let index = start + piece * 64 + square.0 as usize;
                let weight = weights[index].round();
                let weight = if weight.is_nan() { 0.0 } else { weight };
                print!("{:>3}, ", weight);
            }
            println!();
        }
        println!();
    }
}

fn load_positions(path: String, positions: &mut Vec<Position>) {
    println!("Preparing positions from {}...", path);
    parser::parse_epd_file(&path)
        .into_iter()
        .map(|(board, label)| Position::new(board, label))
        .for_each(|position| positions.push(position));
}

fn get_optimal_k(positions: &[Position], weights: &[f32]) -> f32 {
    println!("Finding optimal k...");
    let k = tuner::find_optimal_k(&positions, &weights);
    println!("Optimal k: {}", k);
    k
}

fn get_weights() -> Vec<f32> {
    let mut weights = vec![10.0; SIZE_FEATURES];
    for (piece, value) in [100.0, 300.0, 325.0, 500.0, 900.0].iter().enumerate() {
        weights[IDX_MATERIAL_MG + piece] = *value;
        weights[IDX_MATERIAL_EG + piece] = *value;
    }
    weights
}

mod index {
    pub const IDX_MATERIAL_MG: usize = 0;
    pub const SIZE_MATERIAL_MG: usize = IDX_MATERIAL_MG + 5;

    pub const IDX_MATERIAL_EG: usize = SIZE_MATERIAL_MG;
    pub const SIZE_MATERIAL_EG: usize = IDX_MATERIAL_EG + 5;

    pub const IDX_PSQT_MG: usize = SIZE_MATERIAL_EG;
    pub const SIZE_PSQT_MG: usize = IDX_PSQT_MG + 64 * 6;

    pub const IDX_PSQT_EG: usize = SIZE_PSQT_MG;
    pub const SIZE_PSQT_EG: usize = IDX_PSQT_EG + 64 * 6;

    pub const SIZE_FEATURES: usize = SIZE_PSQT_EG;
}
