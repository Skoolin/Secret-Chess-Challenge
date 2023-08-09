use chess::{Piece, Square};

use crate::index::*;

pub fn print(weights: &[f32]) {
    println!();
    print_material(weights, IDX_MATERIAL_MG, "MG");
    print_material(weights, IDX_MATERIAL_EG, "EG");

    println!();
    print_psqt(weights, IDX_PSQT_MG, "MG");
    print_psqt(weights, IDX_PSQT_EG, "EG");
}

fn print_material(weights: &[f32], start: usize, name: &str) {
    println!("// Material {}", name);
    for piece in 0..5 {
        let index = start + piece;
        print!("{:>3}, ", weights[index].round());
    }
    println!();
}

fn print_psqt(weights: &[f32], start: usize, name: &str) {
    for piece in 0..6 {
        println!("// {:?} {}", Piece::from(piece), name);
        for row in (0..8).rev() {
            for col in 0..8 {
                let square = Square::from_rank_file(row, col);
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
