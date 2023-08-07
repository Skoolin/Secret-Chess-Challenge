use crate::types::*;

const LEARNING_RATE: f32 = 3.0;

pub fn find_optimal_k(positions: &[Position], weights: &[f32]) -> f32 {
    let mut best_k = 0.0;
    let mut best_mse = std::f32::MAX;

    for k in 0..200 {
        let k = k as f32 / 10.0;
        let mse = mean_squared_error(positions, weights, k);
        if mse < best_mse {
            best_k = k;
            best_mse = mse;
        } else {
            break;
        }
    }

    best_k
}

pub fn tune(positions: &[Position], weights: &mut [f32], epochs: usize, k: f32) {
    println!("Starting tuning...");
    let now = std::time::Instant::now();

    for epoch in 0..epochs {
        let mse = mean_squared_error(positions, weights, k);
        println!("#{:<3} MSE: {:.8}", epoch + 1, mse);

        let gradient = compute_gradient(positions, weights, k);
        for index in 0..weights.len() {
            weights[index] += LEARNING_RATE * gradient[index];
        }
    }

    println!("Tuning finished in {:?}", now.elapsed());
}

fn compute_gradient(positions: &[Position], weights: &mut [f32], k: f32) -> Vec<f32> {
    let mut gradient = vec![0.0; weights.len()];
    let mut counts = vec![0; weights.len()];

    for position in positions {
        let prediction = predict(position, weights, k);
        let error = position.label - prediction;

        for feature in &position.features {
            gradient[feature.index] += error * feature.coefficient * weights[feature.index];
            counts[feature.index] += 1;
        }
    }

    for index in 0..gradient.len() {
        gradient[index] /= counts[index] as f32;
    }

    gradient
}

fn predict(position: &Position, weights: &[f32], k: f32) -> f32 {
    let mut prediction = 0.0;
    for feature in &position.features {
        prediction += feature.coefficient * weights[feature.index];
    }
    sigmoid(prediction, k)
}

fn sigmoid(x: f32, k: f32) -> f32 {
    1.0 / (1.0 + 10f32.powf(-k * x / 400.0))
}

fn mean_squared_error(positions: &[Position], weights: &[f32], k: f32) -> f32 {
    let mut error = 0.0;
    for position in positions {
        let prediction = predict(position, weights, k);
        error += (position.label - prediction).powi(2);
    }
    error / positions.len() as f32
}
