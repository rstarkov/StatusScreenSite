const path = require('path');
const ManifestPlugin = require('webpack-manifest-plugin');
const DynamicCdnWebpackPlugin = require('dynamic-cdn-webpack-plugin');
const ExtractTextPlugin = require("extract-text-webpack-plugin");
const LessPluginAutoPrefix = require('less-plugin-autoprefix');

const extractLess = new ExtractTextPlugin({
  filename: "app.css"
});

module.exports = {

  entry: './Src/App.ts',

  output: {
    filename: 'app.js',
    path: path.resolve(__dirname, '../Builds/Static')
  },

  resolve: {
    extensions: ['.ts', '.tsx', '.js', '.jsx', '.less']
  },

  module: {
    loaders: [
      {
        test: /\.tsx?$/,
        loader: 'awesome-typescript-loader'
      },
      {
        test: /\.less$/,
        use: extractLess.extract({
          use: [{
            loader: "css-loader"
          }, {
            loader: 'less-loader',
            options: {
              plugins: [
                new LessPluginAutoPrefix({browsers: ["last 3 versions"]})
              ]
            }
          }]
        })
      }
    ]
  },

  plugins: [
    new ManifestPlugin({
      fileName: 'manifest.json'
    }),
    new DynamicCdnWebpackPlugin({
      resolver: function (modulePath, version) {
        if (modulePath == 'lodash')
          return { name: 'lodash', url: 'https://cdnjs.cloudflare.com/ajax/libs/lodash.js/' + version + '/lodash.min.js', var: '_' };
        else if (modulePath == 'jquery')
          return { name: 'jquery', url: 'https://cdnjs.cloudflare.com/ajax/libs/jquery/' + version + '/jquery.min.js', var: '$' };
        //else if (modulePath == 'plottable')
        //  return { name: 'plottable', url: 'https://cdnjs.cloudflare.com/ajax/libs/plottable.js/' + version + '/plottable.min.js', var: 'plottable' };
        else if (modulePath == 'd3')
          return { name: 'd3', url: 'https://cdnjs.cloudflare.com/ajax/libs/d3/' + version + '/d3.min.js', var: 'd3' };
        else
          return null;
      }
    }),
    extractLess
  ]
};
